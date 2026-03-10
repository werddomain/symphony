using System.Text.Json;
using Microsoft.AspNetCore.Components;
using xLNX.Core.Models;
using xLNX.Core.Orchestration;

namespace xLNX.Web.Components.Pages;

public partial class Dashboard : ComponentBase, IDisposable
{
    private const int RefreshIntervalMs = 5000;

    [Inject] private Func<OrchestratorState> StateProvider { get; set; } = default!;
    [Inject] private Orchestrator Orchestrator { get; set; } = default!;

    private OrchestratorState? _state;
    private string? _error;
    private Timer? _timer;

    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    protected override void OnInitialized()
    {
        RefreshState();
        _timer = new Timer(_ => InvokeAsync(() => { RefreshState(); StateHasChanged(); }), null, RefreshIntervalMs, RefreshIntervalMs);
    }

    private Task RefreshDataAsync()
    {
        RefreshState();
        return Task.CompletedTask;
    }

    private async Task TriggerPollAsync()
    {
        try
        {
            Orchestrator.TriggerPoll();
            _error = null;
        }
        catch (Exception ex)
        {
            _error = $"Failed to trigger poll: {ex.Message}";
        }
        // Refresh after short delay to show updated data
        await Task.Delay(1000);
        RefreshState();
    }

    private void RefreshState()
    {
        try
        {
            _state = StateProvider();
            _error = null;
        }
        catch (Exception ex)
        {
            _error = $"Failed to load state: {ex.Message}";
        }
    }

    private long GetTotalTokens() => _state?.CodexTotals.TotalTokens ?? 0;

    private double GetSecondsRunning()
    {
        if (_state is null) return 0;
        var activeSeconds = _state.Running.Values.Sum(r => (DateTime.UtcNow - r.StartedAt).TotalSeconds);
        return _state.CodexTotals.SecondsRunning + activeSeconds;
    }

    private static string FormatTokens(long tokens)
    {
        if (tokens >= 1_000_000) return $"{tokens / 1_000_000.0:F1}M";
        if (tokens >= 1_000) return $"{tokens / 1_000.0:F1}K";
        return tokens.ToString("N0");
    }

    private static string FormatRuntime(double seconds)
    {
        if (seconds < 60) return $"{seconds:F0}s";
        if (seconds < 3600) return $"{seconds / 60:F1}m";
        return $"{seconds / 3600:F1}h";
    }

    private static string FormatTimeAgo(DateTime utcTime)
    {
        var elapsed = DateTime.UtcNow - utcTime;
        if (elapsed.TotalSeconds < 60) return $"{elapsed.TotalSeconds:F0}s ago";
        if (elapsed.TotalMinutes < 60) return $"{elapsed.TotalMinutes:F0}m ago";
        return $"{elapsed.TotalHours:F1}h ago";
    }

    private static string FormatDueAt(long dueAtMs)
    {
        var due = DateTimeOffset.FromUnixTimeMilliseconds(dueAtMs);
        var remaining = due - DateTimeOffset.UtcNow;
        if (remaining.TotalSeconds <= 0) return "now";
        if (remaining.TotalSeconds < 60) return $"in {remaining.TotalSeconds:F0}s";
        return $"in {remaining.TotalMinutes:F0}m";
    }

    private static string TruncateSessionId(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return "—";
        return sessionId.Length > 16 ? sessionId[..16] + "…" : sessionId;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
