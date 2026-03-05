using Microsoft.Extensions.Logging;
using xLNX.Core.Configuration;
using xLNX.Core.Models;
using xLNX.Core.Tracker;
using xLNX.Core.Workspaces;

namespace xLNX.Core.Orchestration;

/// <summary>
/// Owns the poll tick, in-memory runtime state, and dispatch/reconciliation logic.
/// See SPEC Sections 7, 8, and 16.
/// </summary>
public class Orchestrator
{
    private readonly Func<ServiceConfig> _configProvider;
    private readonly IIssueTrackerClient _tracker;
    private readonly WorkspaceManager _workspaceManager;
    private readonly ILogger<Orchestrator> _logger;
    private readonly OrchestratorState _state;
    private CancellationTokenSource? _cts;

    public Orchestrator(
        Func<ServiceConfig> configProvider,
        IIssueTrackerClient tracker,
        WorkspaceManager workspaceManager,
        ILogger<Orchestrator> logger)
    {
        _configProvider = configProvider;
        _tracker = tracker;
        _workspaceManager = workspaceManager;
        _logger = logger;

        var config = configProvider();
        _state = new OrchestratorState
        {
            PollIntervalMs = config.PollIntervalMs,
            MaxConcurrentAgents = config.MaxConcurrentAgents
        };
    }

    /// <summary>Gets a snapshot of the current orchestrator state.</summary>
    public OrchestratorState State => _state;

    /// <summary>
    /// Starts the orchestration loop.
    /// </summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var config = _configProvider();
        var validation = ConfigLayer.Validate(config);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                _logger.LogError("Config validation failed: {Error}", error);
            }
            throw new InvalidOperationException("Configuration validation failed at startup");
        }

        // Startup terminal workspace cleanup (SPEC 8.6)
        await PerformStartupCleanupAsync(_cts.Token);

        // Start polling loop
        await RunPollLoopAsync(_cts.Token);
    }

    /// <summary>
    /// Stops the orchestration loop.
    /// </summary>
    public void Stop()
    {
        _cts?.Cancel();
    }

    private async Task RunPollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await OnTickAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during poll tick");
            }

            try
            {
                await Task.Delay(_state.PollIntervalMs, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Single poll tick: reconcile, validate, fetch, dispatch.
    /// See SPEC Section 16.2.
    /// </summary>
    public async Task OnTickAsync(CancellationToken ct)
    {
        // Reconcile running issues
        await ReconcileRunningIssuesAsync(ct);

        // Validate config
        var config = _configProvider();
        var validation = ConfigLayer.Validate(config);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                _logger.LogError("Dispatch skipped - config validation: {Error}", error);
            }
            return;
        }

        // Update runtime settings
        _state.PollIntervalMs = config.PollIntervalMs;
        _state.MaxConcurrentAgents = config.MaxConcurrentAgents;

        // Fetch candidates
        List<Issue> candidates;
        try
        {
            candidates = await _tracker.FetchCandidateIssuesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tracker candidate fetch failed, skipping dispatch");
            return;
        }

        // Sort and dispatch
        var sorted = DispatchSorter.Sort(candidates);
        foreach (var issue in sorted)
        {
            if (AvailableSlots() <= 0) break;
            if (ShouldDispatch(issue, config))
            {
                DispatchIssue(issue, attempt: null);
            }
        }
    }

    /// <summary>
    /// Determines if an issue is eligible for dispatch.
    /// See SPEC Section 8.2.
    /// </summary>
    public bool ShouldDispatch(Issue issue, ServiceConfig config)
    {
        if (string.IsNullOrEmpty(issue.Id) || string.IsNullOrEmpty(issue.Identifier)
            || string.IsNullOrEmpty(issue.Title) || string.IsNullOrEmpty(issue.State))
            return false;

        var normalizedState = issue.State.Trim().ToLowerInvariant();

        if (!config.ActiveStates.Any(s => s.Trim().ToLowerInvariant() == normalizedState))
            return false;
        if (config.TerminalStates.Any(s => s.Trim().ToLowerInvariant() == normalizedState))
            return false;

        if (_state.Running.ContainsKey(issue.Id) || _state.Claimed.Contains(issue.Id))
            return false;

        if (AvailableSlots() <= 0) return false;

        // Per-state concurrency
        if (config.MaxConcurrentAgentsByState.TryGetValue(normalizedState, out var stateLimit))
        {
            var runningInState = _state.Running.Values.Count(r =>
                r.Issue.State.Trim().ToLowerInvariant() == normalizedState);
            if (runningInState >= stateLimit) return false;
        }

        // Blocker rule for Todo
        if (normalizedState == "todo" && issue.BlockedBy.Count > 0)
        {
            var terminalStatesNormalized = config.TerminalStates
                .Select(s => s.Trim().ToLowerInvariant()).ToHashSet();

            bool hasNonTerminalBlocker = issue.BlockedBy.Any(b =>
                b.State == null || !terminalStatesNormalized.Contains(b.State.Trim().ToLowerInvariant()));

            if (hasNonTerminalBlocker) return false;
        }

        return true;
    }

    /// <summary>
    /// Returns the number of available global concurrency slots.
    /// </summary>
    public int AvailableSlots()
    {
        return Math.Max(_state.MaxConcurrentAgents - _state.Running.Count, 0);
    }

    private void DispatchIssue(Issue issue, int? attempt)
    {
        _logger.LogInformation("Dispatching issue {IssueId} ({Identifier})", issue.Id, issue.Identifier);

        _state.Running[issue.Id] = new RunningEntry
        {
            Identifier = issue.Identifier,
            Issue = issue,
            RetryAttempt = attempt,
            StartedAt = DateTime.UtcNow
        };
        _state.Claimed.Add(issue.Id);
        _state.RetryAttempts.Remove(issue.Id);
    }

    private async Task ReconcileRunningIssuesAsync(CancellationToken ct)
    {
        if (_state.Running.Count == 0) return;

        var config = _configProvider();

        // Stall detection (SPEC 8.5 Part A)
        if (config.CodexStallTimeoutMs > 0)
        {
            var now = DateTime.UtcNow;
            var stalled = new List<string>();
            foreach (var (issueId, entry) in _state.Running)
            {
                var lastActivity = entry.Session.LastCodexTimestamp ?? entry.StartedAt;
                var elapsed = (now - lastActivity).TotalMilliseconds;
                if (elapsed > config.CodexStallTimeoutMs)
                {
                    stalled.Add(issueId);
                }
            }

            foreach (var issueId in stalled)
            {
                _logger.LogWarning("Stalled session detected for issue {IssueId}", issueId);
                TerminateRunningIssue(issueId, cleanupWorkspace: false);
            }
        }

        // Tracker state refresh (SPEC 8.5 Part B)
        var runningIds = _state.Running.Keys.ToList();
        if (runningIds.Count == 0) return;

        try
        {
            var refreshed = await _tracker.FetchIssueStatesByIdsAsync(runningIds, ct);
            var terminalStates = config.TerminalStates.Select(s => s.Trim().ToLowerInvariant()).ToHashSet();
            var activeStates = config.ActiveStates.Select(s => s.Trim().ToLowerInvariant()).ToHashSet();

            foreach (var issue in refreshed)
            {
                if (!_state.Running.ContainsKey(issue.Id)) continue;

                var normalizedState = issue.State.Trim().ToLowerInvariant();

                if (terminalStates.Contains(normalizedState))
                {
                    TerminateRunningIssue(issue.Id, cleanupWorkspace: true);
                }
                else if (activeStates.Contains(normalizedState))
                {
                    _state.Running[issue.Id].Issue = issue;
                }
                else
                {
                    TerminateRunningIssue(issue.Id, cleanupWorkspace: false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "State refresh failed, keeping workers running");
        }
    }

    private void TerminateRunningIssue(string issueId, bool cleanupWorkspace)
    {
        if (_state.Running.Remove(issueId, out var entry))
        {
            _logger.LogInformation("Terminated issue {IssueId} ({Identifier}), cleanup={Cleanup}",
                issueId, entry.Identifier, cleanupWorkspace);

            var elapsed = (DateTime.UtcNow - entry.StartedAt).TotalSeconds;
            _state.CodexTotals.SecondsRunning += elapsed;
            _state.Claimed.Remove(issueId);
        }
    }

    private async Task PerformStartupCleanupAsync(CancellationToken ct)
    {
        var config = _configProvider();
        try
        {
            var terminalIssues = await _tracker.FetchIssuesByStatesAsync(config.TerminalStates, ct);
            foreach (var issue in terminalIssues)
            {
                await _workspaceManager.RemoveWorkspaceAsync(issue.Identifier, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Startup terminal workspace cleanup failed, continuing");
        }
    }

    /// <summary>
    /// Calculates retry backoff delay.
    /// See SPEC Section 8.4.
    /// </summary>
    public static int CalculateBackoffDelay(int attempt, bool isContinuation, int maxBackoffMs)
    {
        if (isContinuation) return 1_000;
        var delay = (int)Math.Min(10_000 * Math.Pow(2, attempt - 1), maxBackoffMs);
        return delay;
    }
}
