using xLNX.Core.Models;
using xLNX.Core.Orchestration;

namespace xLNX.Tests;

[TestClass]
public class OrchestratorTests
{
    [TestMethod]
    public void CalculateBackoffDelay_Continuation_Returns1000()
    {
        var delay = Orchestrator.CalculateBackoffDelay(1, isContinuation: true, maxBackoffMs: 300_000);
        Assert.AreEqual(1_000, delay);
    }

    [TestMethod]
    public void CalculateBackoffDelay_FirstRetry_Returns10000()
    {
        var delay = Orchestrator.CalculateBackoffDelay(1, isContinuation: false, maxBackoffMs: 300_000);
        Assert.AreEqual(10_000, delay);
    }

    [TestMethod]
    public void CalculateBackoffDelay_SecondRetry_Returns20000()
    {
        var delay = Orchestrator.CalculateBackoffDelay(2, isContinuation: false, maxBackoffMs: 300_000);
        Assert.AreEqual(20_000, delay);
    }

    [TestMethod]
    public void CalculateBackoffDelay_CappedAtMax()
    {
        var delay = Orchestrator.CalculateBackoffDelay(20, isContinuation: false, maxBackoffMs: 300_000);
        Assert.AreEqual(300_000, delay);
    }

    [TestMethod]
    public void ShouldDispatch_ValidIssue_ReturnsTrue()
    {
        var orchestrator = CreateTestOrchestrator();
        var config = new ServiceConfig
        {
            TrackerKind = "linear",
            TrackerApiKey = "key",
            TrackerProjectSlug = "slug",
            ActiveStates = ["Todo", "In Progress"],
            TerminalStates = ["Done", "Cancelled"],
            MaxConcurrentAgents = 10
        };

        var issue = new Issue
        {
            Id = "1",
            Identifier = "TEST-1",
            Title = "Test Issue",
            State = "Todo"
        };

        Assert.IsTrue(orchestrator.ShouldDispatch(issue, config));
    }

    [TestMethod]
    public void ShouldDispatch_TerminalState_ReturnsFalse()
    {
        var orchestrator = CreateTestOrchestrator();
        var config = new ServiceConfig
        {
            ActiveStates = ["Todo"],
            TerminalStates = ["Done"]
        };

        var issue = new Issue
        {
            Id = "1",
            Identifier = "TEST-1",
            Title = "Test",
            State = "Done"
        };

        Assert.IsFalse(orchestrator.ShouldDispatch(issue, config));
    }

    [TestMethod]
    public void ShouldDispatch_MissingFields_ReturnsFalse()
    {
        var orchestrator = CreateTestOrchestrator();
        var config = new ServiceConfig { ActiveStates = ["Todo"] };

        var issue = new Issue { Id = "1" }; // missing identifier, title, state
        Assert.IsFalse(orchestrator.ShouldDispatch(issue, config));
    }

    [TestMethod]
    public void ShouldDispatch_TodoWithNonTerminalBlocker_ReturnsFalse()
    {
        var orchestrator = CreateTestOrchestrator();
        var config = new ServiceConfig
        {
            ActiveStates = ["Todo"],
            TerminalStates = ["Done"]
        };

        var issue = new Issue
        {
            Id = "1",
            Identifier = "TEST-1",
            Title = "Blocked",
            State = "Todo",
            BlockedBy = [new BlockerRef { Id = "2", State = "In Progress" }]
        };

        Assert.IsFalse(orchestrator.ShouldDispatch(issue, config));
    }

    [TestMethod]
    public void ShouldDispatch_TodoWithTerminalBlocker_ReturnsTrue()
    {
        var orchestrator = CreateTestOrchestrator();
        var config = new ServiceConfig
        {
            ActiveStates = ["Todo"],
            TerminalStates = ["Done"],
            MaxConcurrentAgents = 10
        };

        var issue = new Issue
        {
            Id = "1",
            Identifier = "TEST-1",
            Title = "Unblocked",
            State = "Todo",
            BlockedBy = [new BlockerRef { Id = "2", State = "Done" }]
        };

        Assert.IsTrue(orchestrator.ShouldDispatch(issue, config));
    }

    private static Orchestrator CreateTestOrchestrator()
    {
        var config = new ServiceConfig { MaxConcurrentAgents = 10 };
        var loggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
        return new Orchestrator(
            () => config,
            new NullTrackerClient(),
            new xLNX.Core.Workspaces.WorkspaceManager(() => config, new Microsoft.Extensions.Logging.Abstractions.NullLogger<xLNX.Core.Workspaces.WorkspaceManager>()),
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<Orchestrator>()
        );
    }
}

/// <summary>
/// Null implementation of IIssueTrackerClient for testing.
/// </summary>
internal class NullTrackerClient : xLNX.Core.Tracker.IIssueTrackerClient
{
    public Task<List<Issue>> FetchCandidateIssuesAsync(CancellationToken ct = default)
        => Task.FromResult(new List<Issue>());

    public Task<List<Issue>> FetchIssuesByStatesAsync(IReadOnlyList<string> stateNames, CancellationToken ct = default)
        => Task.FromResult(new List<Issue>());

    public Task<List<Issue>> FetchIssueStatesByIdsAsync(IReadOnlyList<string> issueIds, CancellationToken ct = default)
        => Task.FromResult(new List<Issue>());
}
