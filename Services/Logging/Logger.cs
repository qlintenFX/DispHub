// SPDX-License-Identifier: GPL-3.0-or-later
namespace DispHub.Services.Logging;

public static class Logger
{
    private static string? _logPath;
    private static readonly object _lock = new();

    public static void Initialize(string filename = "displayhub.log")
    {
        _logPath = GetWritableLogPath(filename);
    }

    public static void Log(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        WriteToLog($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
    }

    public static void LogError(string message, Exception? ex = null)
    {
        if (string.IsNullOrEmpty(message)) return;
        string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {message}";
        if (ex != null)
        {
            entry += $"\r\n  Exception: {ex.Message}\r\n  StackTrace: {ex.StackTrace}";
            if (ex.InnerException != null)
                entry += $"\r\n  Inner: {ex.InnerException.Message}";
        }
        WriteToLog(entry);
    }

    public static string LogPath => _logPath ?? "displayhub.log";

    private static void WriteToLog(string entry)
    {
        lock (_lock)
        {
            string path = _logPath ?? GetWritableLogPath("displayhub.log");
            try
            {
                File.AppendAllText(path, entry + "\r\n");
            }
            catch
            {
                try
                {
                    string altPath = Path.Combine(Path.GetTempPath(), "displayhub.log");
                    File.AppendAllText(altPath, entry + "\r\n");
                    _logPath = altPath;
                }
                catch { }
            }
        }
    }

    private static string GetWritableLogPath(string filename)
    {
        string[] candidates =
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename),
            Path.Combine(Path.GetTempPath(), filename),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DispHub", filename)
        };

        foreach (string path in candidates)
        {
            try
            {
                string? directory = Path.GetDirectoryName(path);
                if (directory != null && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                File.AppendAllText(path, "");
                return path;
            }
            catch { continue; }
        }

        return candidates[0];
    }
}
