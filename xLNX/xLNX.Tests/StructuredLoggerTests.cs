using xLNX.Core.Logging;
using Microsoft.Extensions.Logging;

namespace xLNX.Tests;

/// <summary>
/// Tests for StructuredLogger (SPEC Section 17.6).
/// </summary>
[TestClass]
public class StructuredLoggerTests
{
    [TestMethod]
    public void BeginIssueScope_IncludesIssueIdAndIdentifier()
    {
        var logger = new CapturingLogger();

        using var scope = logger.BeginIssueScope("issue-123", "TEST-456");

        Assert.IsNotNull(scope);
        Assert.IsInstanceOfType<Dictionary<string, object>>(logger.LastScopeState);
        var dict = (Dictionary<string, object>)logger.LastScopeState!;
        Assert.IsTrue(dict.ContainsKey("issue_id"));
        Assert.IsTrue(dict.ContainsKey("issue_identifier"));
        Assert.AreEqual("issue-123", dict["issue_id"]);
        Assert.AreEqual("TEST-456", dict["issue_identifier"]);
    }

    [TestMethod]
    public void BeginSessionScope_IncludesSessionId()
    {
        var logger = new CapturingLogger();

        using var scope = logger.BeginSessionScope("session-789");

        Assert.IsNotNull(scope);
        Assert.IsInstanceOfType<Dictionary<string, object>>(logger.LastScopeState);
        var dict = (Dictionary<string, object>)logger.LastScopeState!;
        Assert.IsTrue(dict.ContainsKey("session_id"));
        Assert.AreEqual("session-789", dict["session_id"]);
    }

    [TestMethod]
    public void BeginIssueScope_ReturnedScopeIsDisposable()
    {
        var logger = new CapturingLogger();
        var scope = logger.BeginIssueScope("id", "identifier");
        // Should not throw
        scope?.Dispose();
    }

    [TestMethod]
    public void BeginSessionScope_ReturnedScopeIsDisposable()
    {
        var logger = new CapturingLogger();
        var scope = logger.BeginSessionScope("session");
        // Should not throw
        scope?.Dispose();
    }
}

/// <summary>
/// A logger that captures scope state for testing.
/// </summary>
internal class CapturingLogger : ILogger
{
    public object? LastScopeState { get; private set; }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        LastScopeState = state;
        return new NoopDisposable();
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
