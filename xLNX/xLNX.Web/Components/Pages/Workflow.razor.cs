using System.Text;
using Microsoft.AspNetCore.Components;
using xLNX.Core.Models;
using xLNX.Core.Workflow;

namespace xLNX.Web.Components.Pages;

public partial class Workflow : ComponentBase
{
    [Inject] private WorkflowWatcher WorkflowWatcher { get; set; } = default!;

    private WorkflowDefinition? _workflow;
    private string _workflowPath = string.Empty;
    private string? _error;

    protected override void OnInitialized()
    {
        RefreshWorkflow();
    }

    private Task RefreshAsync()
    {
        try
        {
            WorkflowWatcher.Reload();
        }
        catch (Exception ex)
        {
            _error = $"Reload failed: {ex.Message}";
        }
        RefreshWorkflow();
        return Task.CompletedTask;
    }

    private void RefreshWorkflow()
    {
        try
        {
            _workflow = WorkflowWatcher.CurrentWorkflow;
            // Get workflow path via reflection on the private field, or show a placeholder
            _workflowPath = GetWorkflowPath();
            _error = null;
        }
        catch (Exception ex)
        {
            _error = $"Failed to load workflow: {ex.Message}";
        }
    }

    private string GetWorkflowPath()
    {
        // Access the workflow path from the watcher using reflection
        var field = typeof(WorkflowWatcher).GetField("_workflowPath",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field?.GetValue(WorkflowWatcher) as string ?? "(unknown)";
    }

    private string FormatYamlConfig()
    {
        if (_workflow is null || _workflow.Config.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        FormatConfigRecursive(sb, _workflow.Config, 0);
        return sb.ToString();
    }

    private static void FormatConfigRecursive(StringBuilder sb, Dictionary<string, object?> config, int indent)
    {
        var prefix = new string(' ', indent * 2);
        foreach (var kv in config)
        {
            FormatValue(sb, kv.Key, kv.Value, indent);
        }
    }

    private static void FormatValue(StringBuilder sb, string key, object? value, int indent)
    {
        var prefix = new string(' ', indent * 2);
        switch (value)
        {
            case Dictionary<string, object?> stringDict:
                sb.AppendLine($"{prefix}{key}:");
                FormatConfigRecursive(sb, stringDict, indent + 1);
                break;
            case IDictionary<object, object> objDict:
                sb.AppendLine($"{prefix}{key}:");
                foreach (var entry in objDict)
                {
                    FormatValue(sb, entry.Key?.ToString() ?? "", entry.Value, indent + 1);
                }
                break;
            case IList<object> list:
                sb.AppendLine($"{prefix}{key}:");
                foreach (var item in list)
                {
                    sb.AppendLine($"{prefix}  - {item}");
                }
                break;
            default:
                sb.AppendLine($"{prefix}{key}: {value}");
                break;
        }
    }
}
