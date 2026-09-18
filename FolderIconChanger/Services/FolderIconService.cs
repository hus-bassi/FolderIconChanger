using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using FolderIconChanger.Helpers;
using FolderIconChanger.Localization;
using FolderIconChanger.Models;

namespace FolderIconChanger.Services;

public sealed record FolderIconResult(bool Success, string Message);

/// <summary>
/// Applies and restores folder icons the way Windows expects:
/// a real .ICO file + a desktop.ini with [.ShellClassInfo]/IconResource,
/// correct file/folder attributes and an Explorer refresh.
/// </summary>
public static class FolderIconService
{
    private const string DesktopIniName = "desktop.ini";
    private const string DefaultIconName = "FolderIcon.ico";

    private static readonly Regex OursPattern =
        new(@"^FolderIcon(?:_\d+)?\.ico$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // ---------------------------------------------------------------- Apply

    public static FolderIconResult Apply(string folderPath, byte[] icoBytes)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return new FolderIconResult(false, Strings.FolderNotFound);
            }

            string desktopIniPath = Path.Combine(folderPath, DesktopIniName);
            var doc = DesktopIniDocument.Load(desktopIniPath);
            string? previousResource = doc?.GetIconResource();

            string iconFileName = ResolveIconFileName(folderPath, doc?.GetIconResource());
            string iconPath = Path.Combine(folderPath, iconFileName);

            // 1. Write the icon.
            try
            {
                Messages.ForceWritable(iconPath);
                File.WriteAllBytes(iconPath, icoBytes);
                File.SetAttributes(iconPath,
                    FileAttributes.Hidden | FileAttributes.System);
            }
            catch
            {
                Messages.TryCleanupPartialIcon(iconPath);
                throw;
            }

            // 2. Create/update desktop.ini preserving unrelated settings.
            var target = doc ?? DesktopIniDocument.CreateNew();
            target.SetIconResource(iconFileName + ",0");
            try
            {
                Messages.ForceWritable(desktopIniPath);
                File.WriteAllBytes(desktopIniPath, target.Serialize());
                File.SetAttributes(desktopIniPath,
                    FileAttributes.Hidden | FileAttributes.System);
            }
            catch
            {
                Messages.TryCleanupPartialIcon(iconPath);
                throw;
            }

            // 3. Folder attributes so Explorer reads desktop.ini.
            FileAttributes folderAttrs = File.GetAttributes(folderPath);
            FileAttributes targetAttrs =
                folderAttrs | FileAttributes.ReadOnly | FileAttributes.System;
            var record = new AppliedFolderRecord
            {
                FolderPath = folderPath,
                IconFileName = iconFileName,
                HadReadOnly = (folderAttrs & FileAttributes.ReadOnly) != 0,
                HadSystemAttribute = (folderAttrs & FileAttributes.System) != 0,
                CreatedDesktopIni = doc is null,
                PreviousIconResource = previousResource,
                AppliedUtc = DateTime.UtcNow
            };

            // Re-applying over an already-customized folder: Explorer caches the icon
            // and won't re-read desktop.ini if nothing about the folder changes.
            // Toggle the attrs (remove then re-add) so Explorer sees the change.
            bool wasCustomized =
                (folderAttrs & FileAttributes.ReadOnly) != 0 &&
                (folderAttrs & FileAttributes.System) != 0;
            if (wasCustomized)
            {
                File.SetAttributes(folderPath,
                    folderAttrs & ~(FileAttributes.ReadOnly | FileAttributes.System));
                File.SetAttributes(folderPath, targetAttrs);
            }
            else
            {
                File.SetAttributes(folderPath, targetAttrs);
            }

            // 4. Remember for safe restore.
            SettingsService.Instance.SaveRecord(record);

            AppLog.Info($"Icon applied: folder='{folderPath}' icon='{iconFileName}'");
            ExplorerRefreshService.RefreshFolder(folderPath, forceIconCacheRebuild: wasCustomized);
            return new FolderIconResult(true, Strings.IconAppliedSuccess);
        }
        catch (UnauthorizedAccessException ex)
        {
            AppLog.Error(ex, "Apply denied.");
            return new FolderIconResult(false, Strings.ErrorAccessDenied);
        }
        catch (IOException ex) when (IsAccessDenied(ex))
        {
            AppLog.Error(ex, "Apply denied.");
            return new FolderIconResult(false, Strings.ErrorAccessDenied);
        }
        catch (IOException ex)
        {
            AppLog.Error(ex, "Apply failed.");
            return new FolderIconResult(false, Strings.ErrorFolderInaccessible);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Apply failed.");
            return new FolderIconResult(false, Strings.ErrorUnexpected);
        }
    }

    // -------------------------------------------------------------- Restore

    public static FolderIconResult Restore(string folderPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return new FolderIconResult(false, Strings.FolderNotFound);
            }

            string desktopIniPath = Path.Combine(folderPath, DesktopIniName);
            var doc = DesktopIniDocument.Load(desktopIniPath);
            string? currentResource = doc?.GetIconResource();
            string? currentFile = FileNameFromResource(currentResource);

            var record = SettingsService.Instance.GetRecord(folderPath);

            bool hasOurs = (currentFile is not null && IsOurs(currentFile)) ||
                           (record is not null &&
                            record.IconFileName is not null &&
                            File.Exists(Path.Combine(folderPath, record.IconFileName)));

            if (!hasOurs && doc is null)
            {
                return new FolderIconResult(false, Strings.NoCustomIconFound);
            }

            // Remove the icon file we created (and never a user's unrelated file).
            string? toDelete = null;
            if (record is not null && record.IconFileName is string recordedFile && IsOurs(recordedFile))
            {
                toDelete = Path.Combine(folderPath, recordedFile);
            }
            else if (currentFile is not null && IsOurs(currentFile))
            {
                toDelete = Path.Combine(folderPath, currentFile);
            }

            // Fix desktop.ini.
            if (doc is not null)
            {
                string? previous = record?.PreviousIconResource;
                if (!string.IsNullOrWhiteSpace(previous))
                {
                    doc.SetIconResource(previous);
                    doc.SaveTo(desktopIniPath);
                }
                else
                {
                    doc.RemoveIconResource();
                    if (doc.IsEffectivelyEmpty)
                    {
                        if (record?.CreatedDesktopIni == true)
                        {
                            Messages.SafeDelete(desktopIniPath);
                        }
                        else
                        {
                            // keep the file, it was authored by something else
                            doc.SaveTo(desktopIniPath);
                        }
                    }
                    else
                    {
                        doc.SaveTo(desktopIniPath);
                    }
                }
            }

            if (toDelete is not null)
            {
                Messages.SafeDelete(toDelete);
            }

            // Restore folder attributes.
            if (record is not null)
            {
                FileAttributes attrs = File.GetAttributes(folderPath);
                FileAttributes restored = attrs;
                if ((restored & FileAttributes.ReadOnly) != 0 && !record.HadReadOnly)
                {
                    restored &= ~FileAttributes.ReadOnly;
                }

                if ((restored & FileAttributes.System) != 0 && !record.HadSystemAttribute)
                {
                    restored &= ~FileAttributes.System;
                }

                if (restored != attrs)
                {
                    File.SetAttributes(folderPath, restored);
                }
            }

            SettingsService.Instance.RemoveRecord(folderPath);
            AppLog.Info($"Icon restored: folder='{folderPath}'");
            ExplorerRefreshService.RefreshFolder(folderPath);
            ExplorerRefreshService.RebuildIconCache();

            return new FolderIconResult(true,
                string.IsNullOrWhiteSpace(record?.PreviousIconResource)
                    ? Strings.IconRestoredSuccess
                    : Strings.RestoredPrevious);
        }
        catch (UnauthorizedAccessException ex)
        {
            AppLog.Error(ex, "Restore denied.");
            return new FolderIconResult(false, Strings.ErrorAccessDenied);
        }
        catch (IOException ex) when (IsAccessDenied(ex))
        {
            AppLog.Error(ex, "Restore denied.");
            return new FolderIconResult(false, Strings.ErrorAccessDenied);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Restore failed.");
            return new FolderIconResult(false, Strings.ErrorUnexpected);
        }
    }

    // ------------------------------------------------------------- helpers

    private static string ResolveIconFileName(string folderPath, string? currentResource)
    {
        string? referencedFile = FileNameFromResource(currentResource);
        if (referencedFile is not null && IsOurs(referencedFile))
        {
            return referencedFile;
        }

        string defaultPath = Path.Combine(folderPath, DefaultIconName);
        if (File.Exists(defaultPath))
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            for (int i = 1; ; i++)
            {
                string candidate = i == 1
                    ? $"FolderIcon_{stamp}.ico"
                    : $"FolderIcon_{stamp}_{i}.ico";
                if (!File.Exists(Path.Combine(folderPath, candidate)))
                {
                    return candidate;
                }
            }
        }

        return DefaultIconName;
    }

    private static string? FileNameFromResource(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string v = value.Trim();
        int comma = v.LastIndexOf(',');
        if (comma > 0)
        {
            string suffix = v[(comma + 1)..].Trim();
            if (int.TryParse(suffix, out _))
            {
                v = v[..comma].Trim();
            }
        }

        return Path.GetFileName(v.Trim());
    }

    private static bool IsOurs(string? fileName) =>
        !string.IsNullOrEmpty(fileName) && OursPattern.IsMatch(fileName);

    private static bool IsAccessDenied(IOException ex) =>
        ex.Message.Contains("access", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("denied", StringComparison.OrdinalIgnoreCase);

    /// <summary>Internal cleanup helpers shared by success/error paths.</summary>
    internal static class Messages
    {
        /// <summary>
        /// Windows treats files labelled Hidden+System as "super hidden": .NET's
        /// WriteAllBytes refuses to overwrite them, so clear those attributes first.
        /// </summary>
        public static void ForceWritable(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            FileAttributes attrs = File.GetAttributes(path);
            const FileAttributes sticky =
                FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System;
            if ((attrs & sticky) != 0)
            {
                File.SetAttributes(path, attrs & ~sticky);
            }
        }

        public static void TryCleanupPartialIcon(string iconPath)
        {
            try
            {
                SafeDelete(iconPath);
            }
            catch
            {
                // best effort
            }
        }

        public static void SafeDelete(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
        }
    }
}

// ------------------------------------------------------------- INI document

/// <summary>
/// Minimal desktop.ini editor that preserves unrelated settings and the
/// original file encoding. Only the IconResource key is touched.
/// </summary>
internal sealed class DesktopIniDocument
{
    private readonly List<string> _lines = new();
    private readonly Encoding _encoding;
    private readonly bool _emitPreamble;

    private DesktopIniDocument(Encoding encoding, bool emitPreamble)
    {
        _encoding = encoding;
        _emitPreamble = emitPreamble;
    }

    public static DesktopIniDocument CreateNew()
    {
        return new DesktopIniDocument(new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            emitPreamble: true);
    }

    public static DesktopIniDocument? Load(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        byte[] bytes = File.ReadAllBytes(path);
        var (encoding, hadBom) = DetectEncoding(bytes);
        string text = encoding.GetString(bytes).TrimStart('\uFEFF');
        var doc = new DesktopIniDocument(encoding, hadBom);
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        foreach (string line in lines)
        {
            doc._lines.Add(line.TrimEnd());
        }

        return doc;
    }

    public string? GetIconResource()
    {
        foreach (var entry in EnumerateShellEntries())
        {
            if (entry.Key != null &&
                string.Equals(entry.Key, "IconResource", StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value?.Trim();
            }
        }

        return null;
    }

    public bool SetIconResource(string value)
    {
        int sectionStart = FindShellSectionStart();
        if (sectionStart < 0)
        {
            _lines.Clear();
            _lines.Add("[.ShellClassInfo]");
            _lines.Add($"IconResource={value}");
            return true;
        }

        for (int i = sectionStart + 1; i < _lines.Count; i++)
        {
            if (IsSectionHeader(_lines[i]))
            {
                break; // next section
            }

            if (TryParseKey(_lines[i], out string? key, out _) &&
                string.Equals(key, "IconResource", StringComparison.OrdinalIgnoreCase))
            {
                _lines[i] = $"IconResource={value}";
                return true;
            }
        }

        _lines.Insert(sectionStart + 1, $"IconResource={value}");
        return true;
    }

    public bool RemoveIconResource()
    {
        int sectionStart = FindShellSectionStart();
        if (sectionStart < 0)
        {
            return false;
        }

        bool removed = false;
        for (int i = sectionStart + 1; i < _lines.Count; i++)
        {
            if (IsSectionHeader(_lines[i]))
            {
                break;
            }

            if (TryParseKey(_lines[i], out string? key, out _) &&
                string.Equals(key, "IconResource", StringComparison.OrdinalIgnoreCase))
            {
                _lines.RemoveAt(i);
                removed = true;
                i--;
            }
        }

        return removed;
    }

    public bool IsEffectivelyEmpty
    {
        get
        {
            foreach (string line in _lines)
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                if (trimmed.StartsWith(';')) continue;
                if (string.Equals(trimmed, "[.ShellClassInfo]", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return false;
            }

            return true;
        }
    }

    public byte[] Serialize()
    {
        string content = string.Join("\r\n", _lines);
        if (content.Length > 0)
        {
            content += "\r\n";
        }

        byte[] body = _encoding.GetBytes(content);
        if (!_emitPreamble)
        {
            return body;
        }

        var preamble = _encoding.GetPreamble();
        var result = new byte[preamble.Length + body.Length];
        preamble.CopyTo(result, 0);
        body.CopyTo(result, preamble.Length);
        return result;
    }

    public void SaveTo(string path)
    {
        FolderIconService.Messages.ForceWritable(path);
        File.WriteAllBytes(path, Serialize());
        File.SetAttributes(path, FileAttributes.Hidden | FileAttributes.System);
    }

    private IEnumerable<(string? Key, string? Value)> EnumerateShellEntries()
    {
        int sectionStart = FindShellSectionStart();
        if (sectionStart < 0)
        {
            yield break;
        }

        for (int i = sectionStart + 1; i < _lines.Count; i++)
        {
            if (IsSectionHeader(_lines[i]))
            {
                yield break;
            }

            if (TryParseKey(_lines[i], out string? key, out string? value))
            {
                yield return (key, value);
            }
        }
    }

    private int FindShellSectionStart()
    {
        for (int i = 0; i < _lines.Count; i++)
        {
            string trimmed = _lines[i].Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                string name = trimmed[1..^1].Trim();
                if (string.Equals(name, ".ShellClassInfo", StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static bool IsSectionHeader(string line)
    {
        string trimmed = line.Trim();
        return trimmed.StartsWith('[') && trimmed.EndsWith(']') && trimmed.Length > 2;
    }

    private static bool TryParseKey(string line, out string? key, out string? value)
    {
        key = null;
        value = null;
        int eq = line.IndexOf('=');
        if (eq <= 0)
        {
            return false;
        }

        key = line[..eq].Trim();
        value = line[(eq + 1)..];
        return key.Length > 0;
    }

    private static (Encoding Encoding, bool HadBom) DetectEncoding(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return (new UTF8Encoding(true), true);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return (new UnicodeEncoding(false, true), true);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return (new UnicodeEncoding(true, true), true);
        }

        try
        {
            _ = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
            return (new UTF8Encoding(false), false);
        }
        catch (DecoderFallbackException)
        {
            if (bytes.Any(b => b == 0))
            {
                return (new UnicodeEncoding(false, false), false);
            }

            return (Encoding.Default, false);
        }
    }
}