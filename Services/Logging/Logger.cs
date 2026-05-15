// SPDX-License-Identifier: GPL-3.0-or-later
namespace DispHub.Services.Logging;

public static class Logger
{
    private const string DefaultLogFileName = "displayhub.log";

    private static string? _logPath;
    private static readonly Lock _lock = new();

    public static void Initialize(string filename = DefaultLogFileName)
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

    public static string LogPath => _logPath ?? DefaultLogFileName;

    private static void WriteToLog(string entry)
    {
        lock (_lock)
        {
            string path = _logPath ?? GetWritableLogPath(DefaultLogFileName);
            try
            {
                File.AppendAllText(path, entry + "\r\n");
            }
            catch
            {
                try
                {
                    string altPath = Path.Combine(Path.GetTempPath(), DefaultLogFileName);
                    File.AppendAllText(altPath, entry + "\r\n");
                    _logPath = altPath;
                }
                catch (IOException)
                {
                    // Last-resort fallback: logging itself must never crash the app.
                }
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
            catch (IOException)
            {
                // This candidate path is not writable; try the next one.
            }
        }

        return candidates[0];
    }
}
