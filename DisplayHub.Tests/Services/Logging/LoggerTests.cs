using System.IO;
using FluentAssertions;
using DisplayHub.Services.Logging;
using Xunit;

namespace DisplayHub.Tests.Services.Logging;

public class LoggerTests : IDisposable
{
    private readonly string _testLogPath;

    public LoggerTests()
    {
        _testLogPath = Path.Combine(Path.GetTempPath(), $"displayhub_test_{Guid.NewGuid()}.log");
    }

    public void Dispose()
    {
        try { if (File.Exists(_testLogPath)) File.Delete(_testLogPath); }
        catch { /* best-effort */ }
    }

    [Fact]
    public void Initialize_SetsLogPath()
    {
        Logger.Initialize("displayhub_test.log");
        Logger.LogPath.Should().NotBeNullOrEmpty();
        Logger.LogPath.Should().EndWith("displayhub_test.log");
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
        File.ReadAllText(Logger.LogPath).Should().Contain("ERROR: Something failed");
    }

    [Fact]
    public void LogError_WithException_IncludesExceptionDetails()
    {
        Logger.Initialize(_testLogPath);
        Logger.LogError("Operation failed", new InvalidOperationException("Test exception"));
        string content = File.ReadAllText(Logger.LogPath);
        content.Should().Contain("ERROR: Operation failed");
        content.Should().Contain("Test exception");
    }

    [Fact]
    public void LogError_WithInnerException_IncludesInnerDetails()
    {
        Logger.Initialize(_testLogPath);
        var inner = new ArgumentException("Inner problem");
        Logger.LogError("Nested failure", new InvalidOperationException("Outer problem", inner));
        string content = File.ReadAllText(Logger.LogPath);
        content.Should().Contain("Outer problem");
        content.Should().Contain("Inner problem");
    }

    [Fact]
    public void Log_EmptyMessage_DoesNotWrite()
    {
        Logger.Initialize(_testLogPath);
        Logger.Log("Marker");
        string before = File.ReadAllText(Logger.LogPath);
        Logger.Log("");
        File.ReadAllText(Logger.LogPath).Should().Be(before);
    }

    [Fact]
    public void Log_NullMessage_DoesNotWrite()
    {
        Logger.Initialize(_testLogPath);
        Logger.Log("Marker");
        string before = File.ReadAllText(Logger.LogPath);
        Logger.Log(null!);
        File.ReadAllText(Logger.LogPath).Should().Be(before);
    }

    [Fact]
    public void Log_MultipleMessages_AppendsToFile()
    {
        Logger.Initialize(_testLogPath);
        Logger.Log("First");
        Logger.Log("Second");
        Logger.Log("Third");
        string content = File.ReadAllText(Logger.LogPath);
        content.Should().Contain("First").And.Contain("Second").And.Contain("Third");
    }
}
