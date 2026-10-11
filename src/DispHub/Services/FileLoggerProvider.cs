using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DispHub.Services;

/// <summary>Writes one log file per day to the logs folder and keeps the last week. Never throws.</summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private const int DaysToKeep = 7;
    private readonly string _directory;
    private readonly BlockingCollection<string> _queue = new(boundedCapacity: 10_000);
    private readonly Thread _writer;

    public FileLoggerProvider(string directory)
    {
        _directory = directory;
        _writer = new Thread(WriteLoop) { IsBackground = true, Name = "DispHub log" };
        _writer.Start();
        DeleteOldFiles();
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
        _queue.CompleteAdding();
        _writer.Join(TimeSpan.FromSeconds(2));
        _queue.Dispose();
    }

    private void Enqueue(string line) => _queue.TryAdd(line);

    private void WriteLoop()
    {
        foreach (string line in _queue.GetConsumingEnumerable())
        {
            try
            {
                Directory.CreateDirectory(_directory);
                string path = Path.Combine(_directory, $"disphub-{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(path, line, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Logging must never take the app down.
            }
        }
    }

    private void DeleteOldFiles()
    {
        try
        {
            if (!Directory.Exists(_directory))
                return;
            foreach (string file in Directory.GetFiles(_directory, "disphub-*.log").Order().SkipLast(DaysToKeep))
                File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        private readonly string _category = category[(category.LastIndexOf('.') + 1)..];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;
            var line = new StringBuilder()
                .Append(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture))
                .Append(' ').Append(logLevel.ToString()[..4].ToUpperInvariant())
                .Append(' ').Append(_category).Append(": ").Append(formatter(state, exception));
            if (exception is not null)
                line.AppendLine().Append(exception);
            provider.Enqueue(line.AppendLine().ToString());
        }
    }
}
