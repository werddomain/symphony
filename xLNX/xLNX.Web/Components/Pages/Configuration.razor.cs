using Microsoft.AspNetCore.Components;
using xLNX.Core.Models;

namespace xLNX.Web.Components.Pages;

public partial class Configuration : ComponentBase
{
    [Inject] private Func<ServiceConfig> ConfigProvider { get; set; } = default!;

    private ServiceConfig? _config;
    private string? _error;

    protected override void OnInitialized()
    {
        RefreshConfig();
    }

    private Task RefreshAsync()
    {
        RefreshConfig();
        return Task.CompletedTask;
    }

    private void RefreshConfig()
    {
        try
        {
            _config = ConfigProvider();
            _error = null;
        }
        catch (Exception ex)
        {
            _error = $"Failed to load configuration: {ex.Message}";
        }
    }

    private static string MaskSecret(string value)
    {
        if (string.IsNullOrEmpty(value)) return "—";
        if (value.StartsWith('$')) return value; // Variable reference, safe to show
        if (value.Length <= 8) return "••••••••";
        return value[..4] + "••••" + value[^4..];
    }

    private string FormatStallTimeout()
    {
        if (_config is null) return "—";
        if (_config.CodexStallTimeoutMs <= 0) return "Disabled";
        return $"{_config.CodexStallTimeoutMs} ms ({_config.CodexStallTimeoutMs / 1000.0}s)";
    }
}
