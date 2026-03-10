using xLNX.Core.Models;

namespace xLNX.Core.Tracker;

/// <summary>
/// Interface for issue tracker adapters.
/// See SPEC Section 11.1.
/// </summary>
public interface IIssueTrackerClient
{
    /// <summary>
    /// Fetches candidate issues in configured active states for the project.
    /// </summary>
    Task<List<Issue>> FetchCandidateIssuesAsync(CancellationToken ct = default);

    /// <summary>
    /// Fetches issues in the specified state names (used for startup terminal cleanup).
    /// </summary>
    Task<List<Issue>> FetchIssuesByStatesAsync(IReadOnlyList<string> stateNames, CancellationToken ct = default);

    /// <summary>
    /// Fetches current issue states by IDs (used for active-run reconciliation).
    /// </summary>
    Task<List<Issue>> FetchIssueStatesByIdsAsync(IReadOnlyList<string> issueIds, CancellationToken ct = default);
}
