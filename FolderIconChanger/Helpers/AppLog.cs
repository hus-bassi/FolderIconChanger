using System.IO;

namespace FolderIconChanger.Helpers;

/// <summary>Simple file based logger. Logs are written to %LocalAppData%\FolderIconChanger\logs\app.log</summary>
public static class AppLog
{
    private static readonly object Sync = new();
    private static string _logDirectory = "";
    private static string _logFile = "";

    public static void Initialize()
    {
        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FolderIconChanger");
        _logDirectory = Path.Combine(baseDir, "logs");
        _logFile = Path.Combine(_logDirectory, "app.log");
        try
        {
            Directory.CreateDirectory(_logDirectory);
        }
        catch
        {
            _logFile = "";
        }
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Error(Exception? ex, string context)
    {
        Write("ERROR", $"{context}{(ex is null ? "" : $" :: {ex.GetType().Name}: {ex.Message}")}");
        if (ex is not null)
        {
            Write("DETAIL", ex.ToString());
        }
    }

    private static void Write(string level, string message)
    {
        if (string.IsNullOrEmpty(_logFile))
        {
            return;
        }

        lock (Sync)
        {
            try
            {
                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
                File.AppendAllText(_logFile, line + Environment.NewLine);
            }
            catch
            {
                // logging must never crash the app
            }
        }
    }
}