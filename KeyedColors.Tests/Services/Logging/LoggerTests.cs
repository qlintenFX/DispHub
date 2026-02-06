using FluentAssertions;
using KeyedColors.Services.Logging;
using Xunit;

namespace KeyedColors.Tests.Services.Logging;

public class LoggerTests : IDisposable
{
    private readonly string _testLogPath;

    public LoggerTests()
    {
        _testLogPath = Path.Combine(Path.GetTempPath(), $"keyedcolors_test_{Guid.NewGuid()}.log");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testLogPath))
                File.Delete(_testLogPath);
        }
        catch
        {
            // Cleanup best effort
        }
    }

    [Fact]
    public void Initialize_SetsLogPath()
    {
        Logger.Initialize("keyedcolors_test.log");

        Logger.LogPath.Should().NotBeNullOrEmpty();
        Logger.LogPath.Should().EndWith("keyedcolors_test.log");
    }

    [Fact]
    public void Log_WritesTimestampedMessage()
    {
        Logger.Initialize(_testLogPath);

        Logger.Log("Test message");

        string content = File.ReadAllText(Logger.LogPath);
        content.Should().Contain("Test message");
        content.Should().MatchRegex(@"\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\]");
    }

    [Fact]
    public void LogError_WritesErrorPrefix()
    {
        Logger.Initialize(_testLogPath);

        Logger.LogError("Something failed");

        string content = File.ReadAllText(Logger.LogPath);
        content.Should().Contain("ERROR: Something failed");
    }

    [Fact]
    public void LogError_WithException_IncludesExceptionDetails()
    {
        Logger.Initialize(_testLogPath);
        var ex = new InvalidOperationException("Test exception");

        Logger.LogError("Operation failed", ex);

        string content = File.ReadAllText(Logger.LogPath);
        content.Should().Contain("ERROR: Operation failed");
        content.Should().Contain("Test exception");
    }

    [Fact]
    public void LogError_WithInnerException_IncludesInnerDetails()
    {
        Logger.Initialize(_testLogPath);
        var inner = new ArgumentException("Inner problem");
        var outer = new InvalidOperationException("Outer problem", inner);

        Logger.LogError("Nested failure", outer);

        string content = File.ReadAllText(Logger.LogPath);
        content.Should().Contain("Outer problem");
        content.Should().Contain("Inner problem");
    }

    [Fact]
    public void Log_EmptyMessage_DoesNotWrite()
    {
        Logger.Initialize(_testLogPath);
        // Write a marker so we can check for NO additional writes
        Logger.Log("Marker");
        string beforeContent = File.ReadAllText(Logger.LogPath);

        Logger.Log("");

        string afterContent = File.ReadAllText(Logger.LogPath);
        afterContent.Should().Be(beforeContent);
    }

    [Fact]
    public void Log_NullMessage_DoesNotWrite()
    {
        Logger.Initialize(_testLogPath);
        Logger.Log("Marker");
        string beforeContent = File.ReadAllText(Logger.LogPath);

        Logger.Log(null!);

        string afterContent = File.ReadAllText(Logger.LogPath);
        afterContent.Should().Be(beforeContent);
    }

    [Fact]
    public void Log_MultipleMessages_AppendsToFile()
    {
        Logger.Initialize(_testLogPath);

        Logger.Log("First");
        Logger.Log("Second");
        Logger.Log("Third");

        string content = File.ReadAllText(Logger.LogPath);
        content.Should().Contain("First");
        content.Should().Contain("Second");
        content.Should().Contain("Third");
    }
}
