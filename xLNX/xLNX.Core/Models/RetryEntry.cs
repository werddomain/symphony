namespace xLNX.Core.Models;

/// <summary>
/// Scheduled retry state for an issue.
/// See SPEC Section 4.1.7.
/// </summary>
public sealed class RetryEntry
{
    public string IssueId { get; set; } = string.Empty;

    /// <summary>Best-effort human ID for status surfaces/logs.</summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>1-based for retry queue.</summary>
    public int Attempt { get; set; }

    /// <summary>Monotonic clock timestamp for when retry is due.</summary>
    public long DueAtMs { get; set; }

    /// <summary>Runtime-specific timer reference.</summary>
    public CancellationTokenSource? TimerHandle { get; set; }

    public string? Error { get; set; }
}
