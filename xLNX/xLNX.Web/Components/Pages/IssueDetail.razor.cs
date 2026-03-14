using Microsoft.AspNetCore.Components;
using xLNX.Core.Models;
using xLNX.Core.Orchestration;

namespace xLNX.Web.Components.Pages;

public partial class IssueDetail : ComponentBase, IDisposable
{
    private const int RefreshIntervalMs = 5000;

    [Parameter] public string Identifier { get; set; } = string.Empty;
    [Inject] private Func<OrchestratorState> StateProvider { get; set; } = default!;
    [Inject] private Orchestrator Orchestrator { get; set; } = default!;

    private RunningEntry? _running;
    private RetryEntry? _retry;
    private string? _error;
    private bool _loaded;
    private Timer? _timer;

    protected override void OnInitialized()
    {
        RefreshState();
        _timer = new Timer(_ => InvokeAsync(() => { RefreshState(); StateHasChanged(); }), null, RefreshIntervalMs, RefreshIntervalMs);
    }

    protected override void OnParametersSet()
    {
        RefreshState();
    }

    private Task RefreshAsync()
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
        await Task.Delay(1000);
        RefreshState();
    }

    private void RefreshState()
    {
        try
        {
            var state = StateProvider();
            _running = state.Running.Values.FirstOrDefault(r => r.Identifier == Identifier);
            _retry = state.RetryAttempts.Values.FirstOrDefault(r => r.Identifier == Identifier);
            _loaded = true;
            _error = null;
        }
        catch (Exception ex)
        {
            _error = $"Failed to load issue details: {ex.Message}";
            _loaded = true;
        }
    }

    private static string FormatElapsed(DateTime startedAt)
    {
        var elapsed = DateTime.UtcNow - startedAt;
        if (elapsed.TotalSeconds < 60) return $"{elapsed.TotalSeconds:F0}s";
        if (elapsed.TotalMinutes < 60) return $"{elapsed.TotalMinutes:F0}m {elapsed.Seconds}s";
        return $"{elapsed.TotalHours:F0}h {elapsed.Minutes}m";
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

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
