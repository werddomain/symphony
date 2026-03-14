using Microsoft.AspNetCore.Components;
using xLNX.Core.Models;

namespace xLNX.Web.Components.Pages;

public partial class Sessions : ComponentBase, IDisposable
{
    private const int RefreshIntervalMs = 5000;

    [Inject] private Func<OrchestratorState> StateProvider { get; set; } = default!;

    private OrchestratorState? _state;
    private string? _error;
    private Timer? _timer;

    protected override void OnInitialized()
    {
        RefreshState();
        _timer = new Timer(_ => InvokeAsync(() => { RefreshState(); StateHasChanged(); }), null, RefreshIntervalMs, RefreshIntervalMs);
    }

    private Task RefreshAsync()
    {
        RefreshState();
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

    private static string TruncateSessionId(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return "—";
        return sessionId.Length > 16 ? sessionId[..16] + "…" : sessionId;
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return "—";
        return value.Length > maxLength ? value[..maxLength] + "…" : value;
    }

    private static string FormatElapsed(DateTime startedAt)
    {
        var elapsed = DateTime.UtcNow - startedAt;
        if (elapsed.TotalSeconds < 60) return $"{elapsed.TotalSeconds:F0}s";
        if (elapsed.TotalMinutes < 60) return $"{elapsed.TotalMinutes:F0}m {elapsed.Seconds}s";
        return $"{elapsed.TotalHours:F0}h {elapsed.Minutes}m";
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
