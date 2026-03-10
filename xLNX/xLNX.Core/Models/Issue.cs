namespace xLNX.Core.Models;

/// <summary>
/// Normalized issue record used by orchestration, prompt rendering, and observability.
/// See SPEC Section 4.1.1.
/// </summary>
public sealed class Issue
{
    /// <summary>Stable tracker-internal ID.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Human-readable ticket key (e.g. ABC-123).</summary>
    public string Identifier { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Lower numbers are higher priority. Null means unknown.</summary>
    public int? Priority { get; set; }

    /// <summary>Current tracker state name.</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>Tracker-provided branch metadata if available.</summary>
    public string? BranchName { get; set; }

    public string? Url { get; set; }

    /// <summary>Normalized to lowercase.</summary>
    public List<string> Labels { get; set; } = [];

    public List<BlockerRef> BlockedBy { get; set; } = [];

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Reference to a blocking issue.
/// </summary>
public sealed class BlockerRef
{
    public string? Id { get; set; }
    public string? Identifier { get; set; }
    public string? State { get; set; }
}
