using Microsoft.Extensions.Logging;

namespace xLNX.Core.Logging;

/// <summary>
/// Structured logging helpers for Symphony-specific context.
/// See SPEC Section 13.1.
/// </summary>
public static class StructuredLogger
{
    /// <summary>
    /// Creates a logging scope with issue context fields.
    /// </summary>
    public static IDisposable? BeginIssueScope(this ILogger logger, string issueId, string issueIdentifier)
    {
        return logger.BeginScope(new Dictionary<string, object>
        {
            ["issue_id"] = issueId,
            ["issue_identifier"] = issueIdentifier
        });
    }

    /// <summary>
    /// Creates a logging scope with session context fields.
    /// </summary>
    public static IDisposable? BeginSessionScope(this ILogger logger, string sessionId)
    {
        return logger.BeginScope(new Dictionary<string, object>
        {
            ["session_id"] = sessionId
        });
    }
}
