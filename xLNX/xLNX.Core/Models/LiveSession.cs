namespace xLNX.Core.Models;

/// <summary>
/// State tracked while a coding-agent subprocess is running.
/// See SPEC Section 4.1.6.
/// </summary>
public sealed class LiveSession
{
    /// <summary>Format: thread_id-turn_id</summary>
    public string SessionId { get; set; } = string.Empty;

    public string ThreadId { get; set; } = string.Empty;
    public string TurnId { get; set; } = string.Empty;
    public string? CodexAppServerPid { get; set; }
    public string? LastCodexEvent { get; set; }
    public DateTime? LastCodexTimestamp { get; set; }
    public string? LastCodexMessage { get; set; }

    // Token counters
    public long CodexInputTokens { get; set; }
    public long CodexOutputTokens { get; set; }
    public long CodexTotalTokens { get; set; }
    public long LastReportedInputTokens { get; set; }
    public long LastReportedOutputTokens { get; set; }
    public long LastReportedTotalTokens { get; set; }

    /// <summary>Number of coding-agent turns started within the current worker lifetime.</summary>
    public int TurnCount { get; set; }
}
