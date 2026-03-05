using Microsoft.Extensions.Logging;
using xLNX.Core.Configuration;
using xLNX.Core.Models;
using xLNX.Core.Tracker;
using xLNX.Core.Workflow;
using xLNX.Core.Workspaces;

namespace xLNX.Core.Agent;

/// <summary>
/// Wraps workspace + prompt + app-server client into one agent attempt.
/// See SPEC Sections 10.7 and 16.5.
/// </summary>
public class AgentRunner
{
    private readonly Func<ServiceConfig> _configProvider;
    private readonly Func<WorkflowDefinition> _workflowProvider;
    private readonly WorkspaceManager _workspaceManager;
    private readonly IIssueTrackerClient _tracker;
    private readonly ILogger<AgentRunner> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public AgentRunner(
        Func<ServiceConfig> configProvider,
        Func<WorkflowDefinition> workflowProvider,
        WorkspaceManager workspaceManager,
        IIssueTrackerClient tracker,
        ILogger<AgentRunner> logger,
        ILoggerFactory loggerFactory)
    {
        _configProvider = configProvider;
        _workflowProvider = workflowProvider;
        _workspaceManager = workspaceManager;
        _tracker = tracker;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Runs a full agent attempt for the given issue.
    /// See SPEC Section 16.5.
    /// </summary>
    public async Task<RunAttempt> RunAsync(Issue issue, int? attempt, Action<string, object?>? onCodexUpdate = null, CancellationToken ct = default)
    {
        var config = _configProvider();
        var workflow = _workflowProvider();
        var runAttempt = new RunAttempt
        {
            IssueId = issue.Id,
            IssueIdentifier = issue.Identifier,
            Attempt = attempt,
            StartedAt = DateTime.UtcNow,
            Status = RunAttemptStatus.PreparingWorkspace
        };

        Workspace workspace;
        try
        {
            workspace = await _workspaceManager.CreateForIssueAsync(issue.Identifier, ct);
            runAttempt.WorkspacePath = workspace.Path;
        }
        catch (Exception ex)
        {
            runAttempt.Status = RunAttemptStatus.Failed;
            runAttempt.Error = $"Workspace error: {ex.Message}";
            return runAttempt;
        }

        // before_run hook
        if (!string.IsNullOrEmpty(config.HookBeforeRun))
        {
            try
            {
                await _workspaceManager.RunHookAsync(config.HookBeforeRun, workspace.Path, config.HookTimeoutMs, "before_run", ct);
            }
            catch (Exception ex)
            {
                runAttempt.Status = RunAttemptStatus.Failed;
                runAttempt.Error = $"before_run hook error: {ex.Message}";
                return runAttempt;
            }
        }

        runAttempt.Status = RunAttemptStatus.LaunchingAgentProcess;

        using var appServer = new AppServerClient(_loggerFactory.CreateLogger<AppServerClient>());
        try
        {
            appServer.Launch(config.CodexCommand, workspace.Path);

            runAttempt.Status = RunAttemptStatus.InitializingSession;
            var threadId = await appServer.StartSessionAsync(
                workspace.Path, config.CodexApprovalPolicy, config.CodexThreadSandbox,
                config.CodexReadTimeoutMs, ct);

            int maxTurns = config.MaxTurns;
            int turnNumber = 1;

            while (turnNumber <= maxTurns)
            {
                runAttempt.Status = RunAttemptStatus.BuildingPrompt;
                string prompt;
                try
                {
                    prompt = turnNumber == 1
                        ? PromptRenderer.Render(workflow.PromptTemplate, issue, attempt)
                        : $"Continue working on {issue.Identifier}: {issue.Title}. This is turn {turnNumber} of {maxTurns}.";
                }
                catch (Exception ex)
                {
                    runAttempt.Status = RunAttemptStatus.Failed;
                    runAttempt.Error = $"Prompt error: {ex.Message}";
                    await RunAfterRunHook(config, workspace.Path, ct);
                    return runAttempt;
                }

                runAttempt.Status = RunAttemptStatus.StreamingTurn;
                var turnId = await appServer.StartTurnAsync(
                    threadId, prompt, workspace.Path,
                    $"{issue.Identifier}: {issue.Title}",
                    config.CodexApprovalPolicy, config.CodexTurnSandboxPolicy,
                    config.CodexReadTimeoutMs, ct);

                onCodexUpdate?.Invoke("session_started", new { sessionId = $"{threadId}-{turnId}", turnCount = turnNumber });

                var result = await appServer.StreamTurnAsync(config.CodexTurnTimeoutMs, msg =>
                {
                    onCodexUpdate?.Invoke("codex_update", msg);
                }, ct);

                if (result is "turn/failed" or "turn/cancelled" or "turn_timeout" or "port_exit")
                {
                    runAttempt.Status = result switch
                    {
                        "turn_timeout" => RunAttemptStatus.TimedOut,
                        _ => RunAttemptStatus.Failed
                    };
                    runAttempt.Error = $"Turn ended: {result}";
                    await RunAfterRunHook(config, workspace.Path, ct);
                    return runAttempt;
                }

                // Check if issue is still active
                try
                {
                    var refreshed = await _tracker.FetchIssueStatesByIdsAsync([issue.Id], ct);
                    if (refreshed.Count > 0) issue = refreshed[0];
                }
                catch (Exception ex)
                {
                    runAttempt.Status = RunAttemptStatus.Failed;
                    runAttempt.Error = $"Issue state refresh error: {ex.Message}";
                    await RunAfterRunHook(config, workspace.Path, ct);
                    return runAttempt;
                }

                var activeNormalized = config.ActiveStates.Select(s => s.Trim().ToLowerInvariant()).ToHashSet();
                if (!activeNormalized.Contains(issue.State.Trim().ToLowerInvariant()))
                    break;

                turnNumber++;
            }

            runAttempt.Status = RunAttemptStatus.Succeeded;
        }
        catch (Exception ex)
        {
            runAttempt.Status = RunAttemptStatus.Failed;
            runAttempt.Error = $"Agent session error: {ex.Message}";
        }

        await RunAfterRunHook(config, workspace.Path, ct);
        return runAttempt;
    }

    private async Task RunAfterRunHook(ServiceConfig config, string workspacePath, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(config.HookAfterRun))
        {
            try
            {
                await _workspaceManager.RunHookAsync(config.HookAfterRun, workspacePath, config.HookTimeoutMs, "after_run", ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "after_run hook failed, ignoring");
            }
        }
    }
}
