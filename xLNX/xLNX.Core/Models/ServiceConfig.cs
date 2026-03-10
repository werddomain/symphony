namespace xLNX.Core.Models;

/// <summary>
/// Typed runtime values derived from WorkflowDefinition.Config.
/// See SPEC Section 4.1.3 and 6.4.
/// </summary>
public sealed class ServiceConfig
{
    // Tracker
    public string TrackerKind { get; set; } = string.Empty;
    public string TrackerEndpoint { get; set; } = "https://api.linear.app/graphql";
    public string TrackerApiKey { get; set; } = string.Empty;
    public string TrackerProjectSlug { get; set; } = string.Empty;
    public List<string> ActiveStates { get; set; } = ["Todo", "In Progress"];
    public List<string> TerminalStates { get; set; } = ["Closed", "Cancelled", "Canceled", "Duplicate", "Done"];

    // Polling
    public int PollIntervalMs { get; set; } = 30_000;

    // Workspace
    public string WorkspaceRoot { get; set; } = Path.Combine(Path.GetTempPath(), "symphony_workspaces");

    // Hooks
    public string? HookAfterCreate { get; set; }
    public string? HookBeforeRun { get; set; }
    public string? HookAfterRun { get; set; }
    public string? HookBeforeRemove { get; set; }
    public int HookTimeoutMs { get; set; } = 60_000;

    // Agent
    public int MaxConcurrentAgents { get; set; } = 10;
    public int MaxTurns { get; set; } = 20;
    public int MaxRetryBackoffMs { get; set; } = 300_000;
    public Dictionary<string, int> MaxConcurrentAgentsByState { get; set; } = new();

    // Codex
    public string CodexCommand { get; set; } = "codex app-server";
    public string? CodexApprovalPolicy { get; set; }
    public string? CodexThreadSandbox { get; set; }
    public string? CodexTurnSandboxPolicy { get; set; }
    public int CodexTurnTimeoutMs { get; set; } = 3_600_000;
    public int CodexReadTimeoutMs { get; set; } = 5_000;
    public int CodexStallTimeoutMs { get; set; } = 300_000;

    // Server (optional extension)
    public int? ServerPort { get; set; }
}
