using xLNX.Core.Models;

namespace xLNX.Core.Orchestration;

/// <summary>
/// Sorts issues for dispatch priority.
/// See SPEC Section 8.2.
/// </summary>
public static class DispatchSorter
{
    /// <summary>
    /// Sorts issues by priority ascending (1..4 preferred, null last),
    /// then by created_at oldest first, then by identifier lexicographic.
    /// </summary>
    public static List<Issue> Sort(IEnumerable<Issue> issues)
    {
        return issues
            .OrderBy(i => i.Priority.HasValue ? 0 : 1)
            .ThenBy(i => i.Priority ?? int.MaxValue)
            .ThenBy(i => i.CreatedAt ?? DateTime.MaxValue)
            .ThenBy(i => i.Identifier, StringComparer.Ordinal)
            .ToList();
    }
}
