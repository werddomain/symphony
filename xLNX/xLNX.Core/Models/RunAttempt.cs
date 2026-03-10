namespace xLNX.Core.Models;

/// <summary>
/// One execution attempt for one issue.
/// See SPEC Section 4.1.5.
/// </summary>
public sealed class RunAttempt
{
    public string IssueId { get; set; } = string.Empty;
    public string IssueIdentifier { get; set; } = string.Empty;

    /// <summary>Null for first run, >=1 for retries/continuation.</summary>
    public int? Attempt { get; set; }

    public string WorkspacePath { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public RunAttemptStatus Status { get; set; } = RunAttemptStatus.PreparingWorkspace;
    public string? Error { get; set; }
}

/// <summary>
/// Run attempt lifecycle phases.
/// See SPEC Section 7.2.
/// </summary>
public enum RunAttemptStatus
{
    PreparingWorkspace,
    BuildingPrompt,
    LaunchingAgentProcess,
    InitializingSession,
    StreamingTurn,
    Finishing,
    Succeeded,
    Failed,
    TimedOut,
    Stalled,
    CanceledByReconciliation
}
