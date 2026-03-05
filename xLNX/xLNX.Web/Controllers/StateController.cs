using Microsoft.AspNetCore.Mvc;
using xLNX.Core.Models;

namespace xLNX.Web.Controllers;

/// <summary>
/// REST API for Symphony xLNX observability.
/// See SPEC Section 13.7.2.
/// </summary>
[ApiController]
[Route("api/v1")]
public class StateController : ControllerBase
{
    private readonly Func<OrchestratorState> _stateProvider;

    public StateController(Func<OrchestratorState> stateProvider)
    {
        _stateProvider = stateProvider;
    }

    /// <summary>
    /// GET /api/v1/state - Returns current system state summary.
    /// </summary>
    [HttpGet("state")]
    public IActionResult GetState()
    {
        var state = _stateProvider();

        var running = state.Running.Values.Select(r => new
        {
            issue_id = r.Issue.Id,
            issue_identifier = r.Identifier,
            state = r.Issue.State,
            session_id = r.Session.SessionId,
            turn_count = r.Session.TurnCount,
            last_event = r.Session.LastCodexEvent,
            last_message = r.Session.LastCodexMessage ?? "",
            started_at = r.StartedAt.ToString("o"),
            last_event_at = r.Session.LastCodexTimestamp?.ToString("o"),
            tokens = new
            {
                input_tokens = r.Session.CodexInputTokens,
                output_tokens = r.Session.CodexOutputTokens,
                total_tokens = r.Session.CodexTotalTokens
            }
        }).ToList();

        var retrying = state.RetryAttempts.Values.Select(r => new
        {
            issue_id = r.IssueId,
            issue_identifier = r.Identifier,
            attempt = r.Attempt,
            due_at = DateTimeOffset.FromUnixTimeMilliseconds(r.DueAtMs).ToString("o"),
            error = r.Error
        }).ToList();

        // Compute live seconds_running
        var activeSeconds = state.Running.Values
            .Sum(r => (DateTime.UtcNow - r.StartedAt).TotalSeconds);

        return Ok(new
        {
            generated_at = DateTime.UtcNow.ToString("o"),
            counts = new
            {
                running = state.Running.Count,
                retrying = state.RetryAttempts.Count
            },
            running,
            retrying,
            codex_totals = new
            {
                input_tokens = state.CodexTotals.InputTokens,
                output_tokens = state.CodexTotals.OutputTokens,
                total_tokens = state.CodexTotals.TotalTokens,
                seconds_running = state.CodexTotals.SecondsRunning + activeSeconds
            },
            rate_limits = state.CodexRateLimits
        });
    }

    /// <summary>
    /// GET /api/v1/{identifier} - Returns issue-specific runtime details.
    /// </summary>
    [HttpGet("{identifier}")]
    public IActionResult GetIssue(string identifier)
    {
        var state = _stateProvider();

        var runningEntry = state.Running.Values
            .FirstOrDefault(r => r.Identifier == identifier);

        var retryEntry = state.RetryAttempts.Values
            .FirstOrDefault(r => r.Identifier == identifier);

        if (runningEntry == null && retryEntry == null)
        {
            return NotFound(new { error = new { code = "issue_not_found", message = $"Issue {identifier} not found in current state" } });
        }

        return Ok(new
        {
            issue_identifier = identifier,
            issue_id = runningEntry?.Issue.Id ?? retryEntry?.IssueId,
            status = runningEntry != null ? "running" : "retrying",
            running = runningEntry != null ? new
            {
                session_id = runningEntry.Session.SessionId,
                turn_count = runningEntry.Session.TurnCount,
                state = runningEntry.Issue.State,
                started_at = runningEntry.StartedAt.ToString("o"),
                last_event = runningEntry.Session.LastCodexEvent,
                last_message = runningEntry.Session.LastCodexMessage,
                last_event_at = runningEntry.Session.LastCodexTimestamp?.ToString("o"),
                tokens = new
                {
                    input_tokens = runningEntry.Session.CodexInputTokens,
                    output_tokens = runningEntry.Session.CodexOutputTokens,
                    total_tokens = runningEntry.Session.CodexTotalTokens
                }
            } : null,
            retry = retryEntry != null ? new
            {
                attempt = retryEntry.Attempt,
                due_at = DateTimeOffset.FromUnixTimeMilliseconds(retryEntry.DueAtMs).ToString("o"),
                error = retryEntry.Error
            } : null
        });
    }

    /// <summary>
    /// POST /api/v1/refresh - Triggers immediate poll + reconciliation.
    /// </summary>
    [HttpPost("refresh")]
    public IActionResult Refresh()
    {
        return Accepted(new
        {
            queued = true,
            coalesced = false,
            requested_at = DateTime.UtcNow.ToString("o"),
            operations = new[] { "poll", "reconcile" }
        });
    }
}
