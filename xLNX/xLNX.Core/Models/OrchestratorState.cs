namespace xLNX.Core.Models;

/// <summary>
/// Single authoritative in-memory state owned by the orchestrator.
/// See SPEC Section 4.1.8.
/// </summary>
public sealed class OrchestratorState
{
    public int PollIntervalMs { get; set; }
    public int MaxConcurrentAgents { get; set; }

    /// <summary>Map: issue_id -> running entry.</summary>
    public Dictionary<string, RunningEntry> Running { get; set; } = new();

    /// <summary>Set of issue IDs reserved/running/retrying.</summary>
    public HashSet<string> Claimed { get; set; } = [];

    /// <summary>Map: issue_id -> RetryEntry.</summary>
    public Dictionary<string, RetryEntry> RetryAttempts { get; set; } = new();

    /// <summary>Bookkeeping only, not dispatch gating.</summary>
    public HashSet<string> Completed { get; set; } = [];

    public CodexTotals CodexTotals { get; set; } = new();
    public object? CodexRateLimits { get; set; }
}

/// <summary>
/// Entry in the running map for one issue.
/// </summary>
public sealed class RunningEntry
{
    public string Identifier { get; set; } = string.Empty;
    public Issue Issue { get; set; } = null!;
    public LiveSession Session { get; set; } = new();
    public int? RetryAttempt { get; set; }
    public DateTime StartedAt { get; set; }
}

/// <summary>
/// Aggregate token and runtime counters.
/// </summary>
public sealed class CodexTotals
{
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public long TotalTokens { get; set; }
    public double SecondsRunning { get; set; }
}
