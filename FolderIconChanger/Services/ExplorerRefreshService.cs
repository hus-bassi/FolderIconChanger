using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using FolderIconChanger.Helpers;

namespace FolderIconChanger.Services;

/// <summary>
/// Asks Windows Explorer to refresh a folder and rebuild the icon cache
/// without restarting explorer.exe.
/// </summary>
public static class ExplorerRefreshService
{
    // SHCNE events
    private const int SHCNE_UPDATEDIR = 0x00001000;
    private const int SHCNE_UPDATEITEM = 0x00002000;
    private const int SHCNE_ICONASSOCIATIONCHANGED = 0x0800;
    private const int SHCNE_ASSOCCHANGED = 0x08000000;

    // SHCNF flags
    private const uint SHCNF_PATHW = 0x0005;
    private const uint SHCNF_IDLIST = 0x0000;
    private const uint SHCNF_FLUSH = 0x1000;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, string? dwItem1, string? dwItem2);

    public static void RefreshFolder(string folderPath, bool forceIconCacheRebuild = false)
    {
        try
        {
            // The folder's icon is displayed inside its PARENT view, so the parent
            // directory must be notified too (this is what usually updates it).
            string root = Path.GetPathRoot(folderPath) ?? folderPath;
            string parent = Path.GetDirectoryName(folderPath.TrimEnd('\\', '/'));
            if (string.IsNullOrEmpty(parent) || string.Equals(parent, folderPath, StringComparison.OrdinalIgnoreCase))
            {
                parent = root;
            }

            Notify(SHCNE_UPDATEDIR, folderPath, null);   // re-scan the folder's own contents
            Notify(SHCNE_UPDATEDIR, parent, null);       // re-scan the parent (shows this folder's icon)
            Notify(SHCNE_UPDATEITEM, folderPath, null);  // refresh this item itself
            Notify(SHCNE_UPDATEITEM, parent, null);
            Notify(SHCNE_ASSOCCHANGED, null, null);      // rebuild the icon cache

            if (forceIconCacheRebuild)
            {
                RebuildIconCache();
            }

            // Explorer can be slow to pick up a re-applied icon; re-announce shortly
            // afterwards so the new icon reliably replaces the cached one.
            _ = Task.Run(async () =>
            {
                await Task.Delay(650);
                Notify(SHCNE_UPDATEDIR, folderPath, null);
                Notify(SHCNE_UPDATEDIR, parent, null);
                Notify(SHCNE_UPDATEITEM, folderPath, null);
                Notify(SHCNE_ASSOCCHANGED, null, null);
            });
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Explorer refresh failed.");
        }
    }

    /// <summary>
    /// Forces Explorer to drop its cached icons and re-extract them. Needed when
    /// re-applying to an already-customized folder: a huge/stale iconcache_*.db may
    /// keep showing the old icon no matter how correct desktop.ini is.
    /// </summary>
    public static void RebuildIconCache()
    {
        try
        {
            SHChangeNotify(SHCNE_ICONASSOCIATIONCHANGED, SHCNF_IDLIST | SHCNF_FLUSH, null, null);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Icon association refresh failed.");
        }

        RunTool("ie4uinit.exe", "-ClearIconCache"); // Windows 11 (22H2+)
        RunTool("ie4uinit.exe", "-show");           // Windows 10 / 11 pre-22H2
    }

    /// <summary>Runs a small shell utility and waits briefly so it finishes before the UI reports success.</summary>
    private static void RunTool(string file, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, file), arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            using var process = Process.Start(psi);
            if (process is not null)
            {
                process.WaitForExit(3000);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, $"Could not run {file} {arguments}.");
        }
    }

    private static void Notify(int eventId, string? item1, string? item2)
    {
        try
        {
            SHChangeNotify(eventId, SHCNF_PATHW | SHCNF_FLUSH, item1, item2);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, $"SHChangeNotify(0x{eventId:X8}) failed.");
        }
    }
}