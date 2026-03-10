using Microsoft.AspNetCore.Components;
using xLNX.Core.Models;
using xLNX.Core.Orchestration;

namespace xLNX.Web.Components.Pages;

public partial class Retries : ComponentBase, IDisposable
{
    [Inject] private Func<OrchestratorState> StateProvider { get; set; } = default!;
    [Inject] private Orchestrator Orchestrator { get; set; } = default!;

    private OrchestratorState? _state;
    private string? _error;
    private Timer? _timer;

    protected override void OnInitialized()
    {
        RefreshState();
        _timer = new Timer(_ => InvokeAsync(() => { RefreshState(); StateHasChanged(); }), null, 5000, 5000);
    }

    private Task RefreshAsync()
    {
        RefreshState();
        return Task.CompletedTask;
    }

    private Task TriggerPollAsync()
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
        Task.Delay(1000).ContinueWith(_ => InvokeAsync(() => { RefreshState(); StateHasChanged(); }));
        return Task.CompletedTask;
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

    private string FormatNextDue()
    {
        if (_state is null || _state.RetryAttempts.Count == 0) return "—";
        var minDue = _state.RetryAttempts.Values.Min(r => r.DueAtMs);
        return FormatTimeRemaining(minDue);
    }

    private static string FormatDueAtTimestamp(long dueAtMs)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(dueAtMs).ToString("yyyy-MM-dd HH:mm:ss");
    }

    private static string FormatTimeRemaining(long dueAtMs)
    {
        var due = DateTimeOffset.FromUnixTimeMilliseconds(dueAtMs);
        var remaining = due - DateTimeOffset.UtcNow;
        if (remaining.TotalSeconds <= 0) return "overdue";
        if (remaining.TotalSeconds < 60) return $"{remaining.TotalSeconds:F0}s";
        if (remaining.TotalMinutes < 60) return $"{remaining.TotalMinutes:F0}m {remaining.Seconds}s";
        return $"{remaining.TotalHours:F0}h {remaining.Minutes}m";
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return "—";
        return value.Length > maxLength ? value[..maxLength] + "…" : value;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
