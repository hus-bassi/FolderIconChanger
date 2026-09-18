using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using FolderIconChanger.Helpers;
using FolderIconChanger.Localization;
using FolderIconChanger.Services;

namespace FolderIconChanger.Cli;

/// <summary>
/// Hidden command-line mode used for automated testing:
///   FolderIconChanger.exe apply  &lt;image&gt; &lt;folder&gt;
///   FolderIconChanger.exe restore  &lt;folder&gt;
/// Writes a result file to %TEMP%\FolderIconChanger_cli_result.json and exits.
/// </summary>
public static class CliRunner
{
    private static readonly string ResultFile =
        Path.Combine(Path.GetTempPath(), "FolderIconChanger_cli_result.json");

    public static bool TryRun(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0)
        {
            return false;
        }

        // Support passing arguments through a file to avoid command-line
        // quoting issues: FolderIconChanger.exe @C:\args.txt
        if (args[0].StartsWith("@"))
        {
            string argFile = args[0][1..];
            if (!File.Exists(argFile))
            {
                return false;
            }

            args = File.ReadAllLines(argFile)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();
            if (args.Length == 0)
            {
                return false;
            }
        }

        string verb = args[0].ToLowerInvariant();
        if (verb == "apply" || verb == "restore")
        {
            AttachConsole();
            exitCode = Run(args);
            return true;
        }

        return false;
    }

    private static int Run(string[] args)
    {
        string verb = args[0].ToLowerInvariant();
        try
        {
            if (verb == "apply")
            {
                if (args.Length < 3)
                {
                    WriteResult(false, Strings.CliArgError);
                    LogAndEcho("apply requires <image> <folder>");
                    return 2;
                }

                string imagePath = Path.GetFullPath(args[1]);
                string folderPath = Path.GetFullPath(args[2]);

                var source = ImageHelper.LoadImage(imagePath);
                var square = IconGenerator.ComposeSquare(source);
                byte[] ico = IconGenerator.BuildIco(square);
                var result = FolderIconService.Apply(folderPath, ico);
                WriteResult(result.Success, result.Message);
                LogAndEcho(result.Message);
                return result.Success ? 0 : 1;
            }

            if (args.Length < 2)
            {
                WriteResult(false, Strings.CliArgError);
                LogAndEcho("restore requires <folder>");
                return 2;
            }

            string folderPath2 = Path.GetFullPath(args[1]);
            var restoreResult = FolderIconService.Restore(folderPath2);
            WriteResult(restoreResult.Success, restoreResult.Message);
            LogAndEcho(restoreResult.Message);
            return restoreResult.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "CLI run failed.");
            WriteResult(false, Strings.ErrorUnexpected);
            return 1;
        }
    }

    private static void WriteResult(bool ok, string message)
    {
        try
        {
            File.WriteAllText(ResultFile,
                JsonSerializer.Serialize(new { ok, message }));
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "Unable to write CLI result file.");
        }
    }

    private static void LogAndEcho(string message)
    {
        AppLog.Info($"CLI: {message}");
        try
        {
            Console.WriteLine(message);
        }
        catch
        {
            // no console available
        }
    }

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(uint dwProcessId);

    private const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;

    private static void AttachConsole()
    {
        try
        {
            _ = AttachConsole(ATTACH_PARENT_PROCESS);
        }
        catch
        {
            // ignore
        }
    }
}