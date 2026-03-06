using System.IO;

namespace DisplayHub.Services.Logging;

/// <summary>
/// Thread-safe application-wide logger with fallback path resolution.
/// </summary>
public static class Logger
{
    private static string? _logPath;
    private static readonly object _lock = new();

    /// <summary>
    /// Initializes the logger with a writable log file path.
    /// Call once at application startup.
    /// </summary>
    public static void Initialize(string filename = "displayhub.log")
    {
        _logPath = GetWritableLogPath(filename);
    }

    /// <summary>
    /// Logs an informational message with timestamp.
    /// </summary>
    public static void Log(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        WriteToLog($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
    }

    /// <summary>
    /// Logs an error message with optional exception details.
    /// </summary>
    public static void LogError(string message, Exception? ex = null)
    {
        if (string.IsNullOrEmpty(message))
            return;

        string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {message}";
        if (ex != null)
        {
            entry += $"\r\n  Exception: {ex.Message}\r\n  StackTrace: {ex.StackTrace}";
            if (ex.InnerException != null)
            {
                entry += $"\r\n  Inner: {ex.InnerException.Message}\r\n  InnerTrace: {ex.InnerException.StackTrace}";
            }
        }

        WriteToLog(entry);
    }

    /// <summary>
    /// Returns the resolved log file path (for error dialogs).
    /// </summary>
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
                // Try temp directory as last resort
                try
                {
                    string altPath = Path.Combine(Path.GetTempPath(), "displayhub.log");
                    File.AppendAllText(altPath, entry + "\r\n");
                    _logPath = altPath;
                }
                catch
                {
                    // Logging must never crash the application
                }
            }
        }
    }

    private static string GetWritableLogPath(string filename)
    {
        string[] candidates =
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename),
            Path.Combine(
                System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName != null
                    ? Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName) ?? ""
                    : "",
                filename),
            Path.Combine(Path.GetTempPath(), filename),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "DisplayHub",
                filename),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                filename)
        };

        foreach (string path in candidates)
        {
            try
            {
                string? directory = Path.GetDirectoryName(path);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(path, "");
                return path;
            }
            catch
            {
                continue;
            }
        }

        return candidates[0];
    }
}
