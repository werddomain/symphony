using System.Text.Json;
using Microsoft.Extensions.Logging;
using xLNX.Core.Agent;
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
    private readonly AgentRunner? _agentRunner;
    private CancellationTokenSource? _cts;
    private readonly SemaphoreSlim _triggerPollSignal = new(0, 1);

    public Orchestrator(
        Func<ServiceConfig> configProvider,
        IIssueTrackerClient tracker,
        WorkspaceManager workspaceManager,
        ILogger<Orchestrator> logger,
        AgentRunner? agentRunner = null)
    {
        _configProvider = configProvider;
        _tracker = tracker;
        _workspaceManager = workspaceManager;
        _logger = logger;
        _agentRunner = agentRunner;

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
    /// Triggers an immediate poll tick (best-effort, coalesced).
    /// See SPEC Section 13.7.2.
    /// </summary>
    public void TriggerPoll()
    {
        try { _triggerPollSignal.Release(); }
        catch (SemaphoreFullException) { /* already triggered, coalesced */ }
    }

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
            _logger.LogWarning("Starting orchestrator loop with invalid config — dispatch will be skipped until WORKFLOW.md is corrected");
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
                // Wait for poll interval or external trigger, whichever comes first
                using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var delayTask = Task.Delay(_state.PollIntervalMs, delayCts.Token);
                var triggerTask = _triggerPollSignal.WaitAsync(delayCts.Token);
                await Task.WhenAny(delayTask, triggerTask);
                await delayCts.CancelAsync();
            }
            catch (OperationCanceledException)
            {
                if (ct.IsCancellationRequested) break;
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

        var entry = new RunningEntry
        {
            Identifier = issue.Identifier,
            Issue = issue,
            RetryAttempt = attempt,
            StartedAt = DateTime.UtcNow
        };
        _state.Running[issue.Id] = entry;
        _state.Claimed.Add(issue.Id);
        _state.RetryAttempts.Remove(issue.Id);

        // Spawn worker task if agent runner is available (SPEC 16.4)
        if (_agentRunner != null)
        {
            SpawnWorker(issue, attempt);
        }
    }

    private void SpawnWorker(Issue issue, int? attempt)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _agentRunner!.RunAsync(issue, attempt, (eventType, payload) =>
                {
                    HandleCodexUpdate(issue.Id, eventType, payload);
                }, _cts?.Token ?? CancellationToken.None);

                if (result.Status == RunAttemptStatus.Succeeded)
                {
                    OnWorkerExit(issue.Id, isNormal: true);
                }
                else
                {
                    OnWorkerExit(issue.Id, isNormal: false, error: result.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker failed for issue {IssueId}", issue.Id);
                OnWorkerExit(issue.Id, isNormal: false, error: ex.Message);
            }
        });
    }

    /// <summary>
    /// Handles a codex update event from a running worker.
    /// Updates session metadata, token counters, and rate limits.
    /// See SPEC Sections 7.3 and 13.5.
    /// </summary>
    internal void HandleCodexUpdate(string issueId, string eventType, object? payload)
    {
        if (!_state.Running.TryGetValue(issueId, out var entry))
            return;

        entry.Session.LastCodexEvent = eventType;
        entry.Session.LastCodexTimestamp = DateTime.UtcNow;

        if (payload is JsonElement json)
        {
            // Extract session info from session_started events
            if (eventType == "session_started" && json.TryGetProperty("sessionId", out var sid))
            {
                entry.Session.SessionId = sid.GetString() ?? string.Empty;
            }

            // Track turn count from session_started events (SPEC 4.1.6)
            if (eventType == "session_started" && json.TryGetProperty("turnCount", out var tc)
                && tc.ValueKind == JsonValueKind.Number)
            {
                entry.Session.TurnCount = tc.GetInt32();
            }

            // Extract PID if available
            if (json.TryGetProperty("codex_app_server_pid", out var pidProp))
            {
                entry.Session.CodexAppServerPid = pidProp.GetString();
            }

            // Extract humanized message
            if (json.TryGetProperty("message", out var msgProp))
            {
                entry.Session.LastCodexMessage = msgProp.GetString();
            }

            // Extract token usage from various payload shapes (SPEC 13.5)
            ExtractTokenUsage(entry, json);

            // Extract rate limits
            if (json.TryGetProperty("rate_limits", out var rateLimits))
            {
                _state.CodexRateLimits = rateLimits;
            }
        }
    }

    /// <summary>
    /// Extracts token counts from codex event payloads.
    /// Prefers absolute thread totals; tracks deltas to avoid double-counting.
    /// See SPEC Section 13.5.
    /// </summary>
    private static void ExtractTokenUsage(RunningEntry entry, JsonElement json)
    {
        // Look for usage in various shapes
        JsonElement usage = default;
        bool found = false;

        // thread/tokenUsage/updated or total_token_usage
        if (json.TryGetProperty("params", out var p))
        {
            if (p.TryGetProperty("total_token_usage", out var ttu))
            {
                usage = ttu;
                found = true;
            }
            else if (p.TryGetProperty("usage", out var u))
            {
                usage = u;
                found = true;
            }
        }

        if (!found && json.TryGetProperty("usage", out var directUsage))
        {
            usage = directUsage;
            found = true;
        }

        if (!found) return;

        long inputTokens = 0, outputTokens = 0, totalTokens = 0;

        if (usage.TryGetProperty("input_tokens", out var it))
            inputTokens = it.GetInt64();
        else if (usage.TryGetProperty("inputTokens", out var it2))
            inputTokens = it2.GetInt64();

        if (usage.TryGetProperty("output_tokens", out var ot))
            outputTokens = ot.GetInt64();
        else if (usage.TryGetProperty("outputTokens", out var ot2))
            outputTokens = ot2.GetInt64();

        if (usage.TryGetProperty("total_tokens", out var tt))
            totalTokens = tt.GetInt64();
        else if (usage.TryGetProperty("totalTokens", out var tt2))
            totalTokens = tt2.GetInt64();
        else
            totalTokens = inputTokens + outputTokens;

        // Absolute totals: compute delta from last reported
        entry.Session.CodexInputTokens = inputTokens;
        entry.Session.CodexOutputTokens = outputTokens;
        entry.Session.CodexTotalTokens = totalTokens;

        entry.Session.LastReportedInputTokens = inputTokens;
        entry.Session.LastReportedOutputTokens = outputTokens;
        entry.Session.LastReportedTotalTokens = totalTokens;
    }

    /// <summary>
    /// Handles worker exit. Schedules continuation or failure retry.
    /// See SPEC Section 16.6.
    /// </summary>
    internal void OnWorkerExit(string issueId, bool isNormal, string? error = null)
    {
        if (!_state.Running.Remove(issueId, out var entry))
            return;

        // Update aggregate runtime totals
        var elapsed = (DateTime.UtcNow - entry.StartedAt).TotalSeconds;
        _state.CodexTotals.SecondsRunning += elapsed;
        _state.CodexTotals.InputTokens += entry.Session.CodexInputTokens;
        _state.CodexTotals.OutputTokens += entry.Session.CodexOutputTokens;
        _state.CodexTotals.TotalTokens += entry.Session.CodexTotalTokens;

        if (isNormal)
        {
            _state.Completed.Add(issueId);
            ScheduleRetry(issueId, 1, entry.Identifier, isContinuation: true, error: null);
            _logger.LogInformation("Worker exited normally for issue {IssueId} ({Identifier}), continuation retry scheduled",
                issueId, entry.Identifier);
        }
        else
        {
            var nextAttempt = (entry.RetryAttempt ?? 0) + 1;
            ScheduleRetry(issueId, nextAttempt, entry.Identifier, isContinuation: false, error: error);
            _logger.LogWarning("Worker exited abnormally for issue {IssueId} ({Identifier}): {Error}",
                issueId, entry.Identifier, error);
        }
    }

    /// <summary>
    /// Schedules a retry for an issue with appropriate backoff.
    /// See SPEC Section 8.4.
    /// </summary>
    internal void ScheduleRetry(string issueId, int attempt, string identifier, bool isContinuation, string? error)
    {
        // Cancel any existing retry timer for the same issue
        if (_state.RetryAttempts.TryGetValue(issueId, out var existing))
        {
            existing.TimerHandle?.Cancel();
        }

        var config = _configProvider();
        var delayMs = CalculateBackoffDelay(attempt, isContinuation, config.MaxRetryBackoffMs);

        var retryCts = new CancellationTokenSource();
        var retryEntry = new RetryEntry
        {
            IssueId = issueId,
            Identifier = identifier,
            Attempt = attempt,
            DueAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + delayMs,
            TimerHandle = retryCts,
            Error = error
        };
        _state.RetryAttempts[issueId] = retryEntry;

        // Schedule the timer
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMs, retryCts.Token);
                await OnRetryTimerAsync(issueId, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // Timer was cancelled
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Retry timer failed for issue {IssueId}", issueId);
            }
        });
    }

    /// <summary>
    /// Handles retry timer firing. Re-fetches candidates and attempts re-dispatch.
    /// See SPEC Section 16.6.
    /// </summary>
    internal async Task OnRetryTimerAsync(string issueId, CancellationToken ct)
    {
        if (!_state.RetryAttempts.Remove(issueId, out var retryEntry))
            return;

        List<Issue> candidates;
        try
        {
            candidates = await _tracker.FetchCandidateIssuesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Retry poll failed for issue {IssueId}", issueId);
            ScheduleRetry(issueId, retryEntry.Attempt + 1, retryEntry.Identifier, isContinuation: false, error: "retry poll failed");
            return;
        }

        var issue = candidates.FirstOrDefault(c => c.Id == issueId);
        if (issue == null)
        {
            // Issue no longer in active candidates, release claim
            _state.Claimed.Remove(issueId);
            _logger.LogInformation("Issue {IssueId} no longer active, releasing claim", issueId);
            return;
        }

        if (AvailableSlots() <= 0)
        {
            ScheduleRetry(issueId, retryEntry.Attempt + 1, issue.Identifier, isContinuation: false, error: "no available orchestrator slots");
            return;
        }

        DispatchIssue(issue, attempt: retryEntry.Attempt);
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
                _logger.LogWarning("Stalled session detected for issue {IssueId}, scheduling retry", issueId);
                if (_state.Running.TryGetValue(issueId, out var stalledEntry))
                {
                    var nextAttempt = (stalledEntry.RetryAttempt ?? 0) + 1;
                    var identifier = stalledEntry.Identifier;

                    // Remove from running + update totals
                    _state.Running.Remove(issueId);
                    var elapsed = (DateTime.UtcNow - stalledEntry.StartedAt).TotalSeconds;
                    _state.CodexTotals.SecondsRunning += elapsed;
                    _state.CodexTotals.InputTokens += stalledEntry.Session.CodexInputTokens;
                    _state.CodexTotals.OutputTokens += stalledEntry.Session.CodexOutputTokens;
                    _state.CodexTotals.TotalTokens += stalledEntry.Session.CodexTotalTokens;

                    // Schedule retry (SPEC 7.3, 8.5)
                    ScheduleRetry(issueId, nextAttempt, identifier, isContinuation: false, error: "stall_timeout");
                }
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

            if (cleanupWorkspace)
            {
                _ = _workspaceManager.RemoveWorkspaceAsync(entry.Identifier).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        _logger.LogWarning(t.Exception, "Workspace cleanup failed for {Identifier}", entry.Identifier);
                }, TaskScheduler.Default);
            }
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
