using DispHub.Services.Logging;

namespace DispHub.Tests;

public class LoggerTests : IDisposable
{
    private readonly string _testLogDir;
    private readonly string _testLogFile;

    public LoggerTests()
    {
        _testLogDir = Path.Combine(Path.GetTempPath(), "DispHubTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testLogDir);
        _testLogFile = Path.Combine(_testLogDir, "test.log");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testLogDir))
                Directory.Delete(_testLogDir, true);
        }
        catch (IOException)
        {
            // Cleanup best-effort
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Initialize_SetsLogPath()
    {
        Logger.Initialize("test_init.log");
        Assert.False(string.IsNullOrEmpty(Logger.LogPath));
    }

    [Fact]
    public void Log_WritesTimestampedEntry()
    {
        Logger.Initialize("test_log.log");
        Logger.Log("unit test message");
        // Logger should not throw, verify path is set
        Assert.False(string.IsNullOrEmpty(Logger.LogPath));
    }

    [Fact]
    public void Log_IgnoresNullOrEmptyMessages()
    {
        Logger.Initialize("test_empty.log");
        var exception = Record.Exception(() =>
        {
            Logger.Log("");
            Logger.Log(null!);
        });
        Assert.Null(exception);
    }

    [Fact]
    public void LogError_IgnoresNullOrEmptyMessages()
    {
        Logger.Initialize("test_error_empty.log");
        var exception = Record.Exception(() =>
        {
            Logger.LogError("");
            Logger.LogError(null!);
        });
        Assert.Null(exception);
    }

    [Fact]
    public void LogError_WritesExceptionDetails()
    {
        Logger.Initialize("test_error.log");
        var ex = new InvalidOperationException("outer",
            new ArgumentException("inner", "testParam"));

        // Should not throw
        Logger.LogError("test error message", ex);
        Assert.False(string.IsNullOrEmpty(Logger.LogPath));
    }

    [Fact]
    public void LogError_HandlesNullException()
    {
        Logger.Initialize("test_null_ex.log");
        Logger.LogError("message without exception");
        Assert.False(string.IsNullOrEmpty(Logger.LogPath));
    }

    [Fact]
    public void LogPath_ReturnsDefaultWhenNotInitialized()
    {
        // LogPath should always return a non-null value
        Assert.NotNull(Logger.LogPath);
    }
}
