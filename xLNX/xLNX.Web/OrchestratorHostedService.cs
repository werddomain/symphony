using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using xLNX.Core.Agent;
using xLNX.Core.Models;
using xLNX.Core.Orchestration;
using xLNX.Core.Tracker;
using xLNX.Core.Workflow;
using xLNX.Core.Workspaces;

namespace xLNX.Web;

/// <summary>
/// Runs the orchestrator as a background service in the ASP.NET Core host.
/// Bridges the web layer to the orchestrator for API endpoints.
/// See SPEC Sections 16.1 and 13.7.
/// </summary>
public sealed class OrchestratorHostedService : BackgroundService
{
    private readonly Orchestrator _orchestrator;
    private readonly WorkflowWatcher _workflowWatcher;
    private readonly ILogger<OrchestratorHostedService> _logger;

    public OrchestratorHostedService(
        Orchestrator orchestrator,
        WorkflowWatcher workflowWatcher,
        ILogger<OrchestratorHostedService> logger)
    {
        _orchestrator = orchestrator;
        _workflowWatcher = workflowWatcher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrchestratorHostedService starting");

        _workflowWatcher.StartWatching();

        try
        {
            await _orchestrator.StartAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("OrchestratorHostedService stopping gracefully");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "OrchestratorHostedService failed");
            throw;
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _orchestrator.Stop();
        _workflowWatcher.Dispose();
        return base.StopAsync(cancellationToken);
    }
}
