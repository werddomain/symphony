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

    [TestMethod]
    public void OnWorkerExit_Normal_SchedulesContinuationRetry()
    {
        var orchestrator = CreateTestOrchestrator();
        var state = orchestrator.State;
        var issue = new Issue { Id = "1", Identifier = "TEST-1", Title = "Test", State = "Todo" };

        // Simulate a running entry
        state.Running["1"] = new RunningEntry
        {
            Identifier = "TEST-1",
            Issue = issue,
            StartedAt = DateTime.UtcNow.AddSeconds(-10)
        };
        state.Claimed.Add("1");

        orchestrator.OnWorkerExit("1", isNormal: true);

        // Running entry should be removed
        Assert.IsFalse(state.Running.ContainsKey("1"));
        // Should be in completed
        Assert.IsTrue(state.Completed.Contains("1"));
        // Should have retry entry with attempt 1 (continuation)
        Assert.IsTrue(state.RetryAttempts.ContainsKey("1"));
        Assert.AreEqual(1, state.RetryAttempts["1"].Attempt);
        // Runtime totals should be updated
        Assert.IsTrue(state.CodexTotals.SecondsRunning >= 9);
    }

    [TestMethod]
    public void OnWorkerExit_Abnormal_SchedulesRetryWithError()
    {
        var orchestrator = CreateTestOrchestrator();
        var state = orchestrator.State;
        var issue = new Issue { Id = "1", Identifier = "TEST-1", Title = "Test", State = "Todo" };

        state.Running["1"] = new RunningEntry
        {
            Identifier = "TEST-1",
            Issue = issue,
            RetryAttempt = null,
            StartedAt = DateTime.UtcNow
        };
        state.Claimed.Add("1");

        orchestrator.OnWorkerExit("1", isNormal: false, error: "turn_failed");

        Assert.IsFalse(state.Running.ContainsKey("1"));
        Assert.IsTrue(state.RetryAttempts.ContainsKey("1"));
        Assert.AreEqual(1, state.RetryAttempts["1"].Attempt);
        Assert.AreEqual("turn_failed", state.RetryAttempts["1"].Error);
    }

    [TestMethod]
    public void OnWorkerExit_Abnormal_IncrementsRetryAttempt()
    {
        var orchestrator = CreateTestOrchestrator();
        var state = orchestrator.State;

        state.Running["1"] = new RunningEntry
        {
            Identifier = "TEST-1",
            Issue = new Issue { Id = "1", Identifier = "TEST-1", Title = "Test", State = "Todo" },
            RetryAttempt = 2,
            StartedAt = DateTime.UtcNow
        };
        state.Claimed.Add("1");

        orchestrator.OnWorkerExit("1", isNormal: false, error: "timeout");

        Assert.AreEqual(3, state.RetryAttempts["1"].Attempt);
    }

    [TestMethod]
    public void HandleCodexUpdate_UpdatesSessionMetadata()
    {
        var orchestrator = CreateTestOrchestrator();
        var state = orchestrator.State;

        state.Running["1"] = new RunningEntry
        {
            Identifier = "TEST-1",
            Issue = new Issue { Id = "1", Identifier = "TEST-1", Title = "Test", State = "In Progress" },
            StartedAt = DateTime.UtcNow
        };

        orchestrator.HandleCodexUpdate("1", "notification", null);

        Assert.AreEqual("notification", state.Running["1"].Session.LastCodexEvent);
        Assert.IsNotNull(state.Running["1"].Session.LastCodexTimestamp);
    }

    [TestMethod]
    public void HandleCodexUpdate_UnknownIssue_NoOp()
    {
        var orchestrator = CreateTestOrchestrator();
        // Should not throw
        orchestrator.HandleCodexUpdate("nonexistent", "turn_completed", null);
    }

    [TestMethod]
    public void ScheduleRetry_CancelsExistingTimer()
    {
        var orchestrator = CreateTestOrchestrator();
        var state = orchestrator.State;

        orchestrator.ScheduleRetry("1", 1, "TEST-1", isContinuation: true, error: null);
        var firstCts = state.RetryAttempts["1"].TimerHandle;

        orchestrator.ScheduleRetry("1", 2, "TEST-1", isContinuation: false, error: "retry");
        Assert.IsTrue(firstCts!.IsCancellationRequested);
        Assert.AreEqual(2, state.RetryAttempts["1"].Attempt);
        Assert.AreEqual("retry", state.RetryAttempts["1"].Error);
    }

    [TestMethod]
    public void OnWorkerExit_AggregatesTokenTotals()
    {
        var orchestrator = CreateTestOrchestrator();
        var state = orchestrator.State;

        var session = new LiveSession
        {
            CodexInputTokens = 100,
            CodexOutputTokens = 50,
            CodexTotalTokens = 150
        };
        state.Running["1"] = new RunningEntry
        {
            Identifier = "TEST-1",
            Issue = new Issue { Id = "1", Identifier = "TEST-1", Title = "Test", State = "Todo" },
            Session = session,
            StartedAt = DateTime.UtcNow
        };
        state.Claimed.Add("1");

        orchestrator.OnWorkerExit("1", isNormal: true);

        Assert.AreEqual(100, state.CodexTotals.InputTokens);
        Assert.AreEqual(50, state.CodexTotals.OutputTokens);
        Assert.AreEqual(150, state.CodexTotals.TotalTokens);
    }

    [TestMethod]
    public async Task Reconcile_NoRunningIssues_IsNoOp()
    {
        var orchestrator = CreateTestOrchestrator();
        // Should complete without errors
        await orchestrator.OnTickAsync(CancellationToken.None);
    }

    [TestMethod]
    public void ShouldDispatch_AlreadyRunning_ReturnsFalse()
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
            Title = "Test",
            State = "Todo"
        };

        // Mark as running
        orchestrator.State.Running["1"] = new RunningEntry
        {
            Identifier = "TEST-1",
            Issue = issue,
            StartedAt = DateTime.UtcNow
        };

        Assert.IsFalse(orchestrator.ShouldDispatch(issue, config));
    }

    [TestMethod]
    public void ShouldDispatch_AlreadyClaimed_ReturnsFalse()
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
            Title = "Test",
            State = "Todo"
        };

        orchestrator.State.Claimed.Add("1");
        Assert.IsFalse(orchestrator.ShouldDispatch(issue, config));
    }

    [TestMethod]
    public void ShouldDispatch_NonActiveState_ReturnsFalse()
    {
        var orchestrator = CreateTestOrchestrator();
        var config = new ServiceConfig
        {
            ActiveStates = ["Todo", "In Progress"],
            TerminalStates = ["Done"],
            MaxConcurrentAgents = 10
        };

        var issue = new Issue
        {
            Id = "1",
            Identifier = "TEST-1",
            Title = "Test",
            State = "Backlog"
        };

        Assert.IsFalse(orchestrator.ShouldDispatch(issue, config));
    }

    [TestMethod]
    public void ShouldDispatch_PerStateConcurrencyLimitReached_ReturnsFalse()
    {
        var orchestrator = CreateTestOrchestrator();
        var config = new ServiceConfig
        {
            ActiveStates = ["In Progress"],
            TerminalStates = ["Done"],
            MaxConcurrentAgents = 10,
            MaxConcurrentAgentsByState = new() { ["in progress"] = 1 }
        };

        // One already running in "In Progress"
        orchestrator.State.Running["existing"] = new RunningEntry
        {
            Identifier = "EXIST-1",
            Issue = new Issue { Id = "existing", Identifier = "EXIST-1", Title = "Running", State = "In Progress" },
            StartedAt = DateTime.UtcNow
        };

        var issue = new Issue
        {
            Id = "2",
            Identifier = "TEST-2",
            Title = "New",
            State = "In Progress"
        };

        Assert.IsFalse(orchestrator.ShouldDispatch(issue, config));
    }

    [TestMethod]
    public void ShouldDispatch_GlobalSlotExhausted_ReturnsFalse()
    {
        var config = new ServiceConfig { MaxConcurrentAgents = 1 };
        var orchestrator = new Orchestrator(
            () => config,
            new NullTrackerClient(),
            new xLNX.Core.Workspaces.WorkspaceManager(() => config, new Microsoft.Extensions.Logging.Abstractions.NullLogger<xLNX.Core.Workspaces.WorkspaceManager>()),
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<Orchestrator>()
        );

        // Fill the single slot
        orchestrator.State.Running["existing"] = new RunningEntry
        {
            Identifier = "EXIST-1",
            Issue = new Issue { Id = "existing", Identifier = "EXIST-1", Title = "Running", State = "Todo" },
            StartedAt = DateTime.UtcNow
        };

        var issue = new Issue
        {
            Id = "2",
            Identifier = "TEST-2",
            Title = "Blocked by slots",
            State = "Todo"
        };

        Assert.IsFalse(orchestrator.ShouldDispatch(issue, config));
    }

    [TestMethod]
    public void AvailableSlots_CorrectlyComputed()
    {
        var orchestrator = CreateTestOrchestrator();
        Assert.AreEqual(10, orchestrator.AvailableSlots());

        orchestrator.State.Running["1"] = new RunningEntry
        {
            Identifier = "TEST-1",
            Issue = new Issue { Id = "1", Identifier = "TEST-1", Title = "Test", State = "Todo" },
            StartedAt = DateTime.UtcNow
        };

        Assert.AreEqual(9, orchestrator.AvailableSlots());
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
