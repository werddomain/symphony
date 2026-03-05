using Microsoft.Extensions.Logging;
using xLNX.Core.Configuration;
using xLNX.Core.Models;

namespace xLNX.Core.Workflow;

/// <summary>
/// Watches WORKFLOW.md for changes and re-applies config/prompt without restart.
/// See SPEC Section 6.2.
/// </summary>
public sealed class WorkflowWatcher : IDisposable
{
    private readonly string _workflowPath;
    private readonly ILogger<WorkflowWatcher> _logger;
    private FileSystemWatcher? _watcher;
    private readonly object _lock = new();

    private WorkflowDefinition _currentWorkflow;
    private ServiceConfig _currentConfig;

    public WorkflowWatcher(string workflowPath, ILogger<WorkflowWatcher> logger)
    {
        _workflowPath = Path.GetFullPath(workflowPath);
        _logger = logger;

        // Initial load
        _currentWorkflow = LoadWorkflow();
        _currentConfig = ConfigLayer.Build(_currentWorkflow.Config);
    }

    /// <summary>Gets the current effective workflow definition.</summary>
    public WorkflowDefinition CurrentWorkflow
    {
        get { lock (_lock) return _currentWorkflow; }
    }

    /// <summary>Gets the current effective service config.</summary>
    public ServiceConfig CurrentConfig
    {
        get { lock (_lock) return _currentConfig; }
    }

    /// <summary>
    /// Starts watching the workflow file for changes.
    /// </summary>
    public void StartWatching()
    {
        var dir = Path.GetDirectoryName(_workflowPath)
            ?? throw new InvalidOperationException($"Cannot determine directory for workflow path: {_workflowPath}");
        var filename = Path.GetFileName(_workflowPath);

        _watcher = new FileSystemWatcher(dir, filename)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;

        _logger.LogInformation("Watching workflow file {Path} for changes", _workflowPath);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        _logger.LogInformation("Workflow file changed, reloading");
        Reload();
    }

    /// <summary>
    /// Manually reloads the workflow file. Called by the watcher or defensively before dispatch.
    /// </summary>
    public void Reload()
    {
        try
        {
            var workflow = LoadWorkflow();
            var config = ConfigLayer.Build(workflow.Config);

            lock (_lock)
            {
                _currentWorkflow = workflow;
                _currentConfig = config;
            }

            _logger.LogInformation("Workflow reloaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invalid workflow reload, keeping last known good configuration");
        }
    }

    private WorkflowDefinition LoadWorkflow()
    {
        if (!File.Exists(_workflowPath))
        {
            throw new FileNotFoundException($"missing_workflow_file: {_workflowPath}");
        }

        var content = File.ReadAllText(_workflowPath);
        return WorkflowLoader.Parse(content);
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}
