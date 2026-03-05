namespace xLNX.Core.Models;

/// <summary>
/// Parsed WORKFLOW.md payload.
/// See SPEC Section 4.1.2.
/// </summary>
public sealed class WorkflowDefinition
{
    /// <summary>YAML front matter root object.</summary>
    public Dictionary<string, object?> Config { get; set; } = new();

    /// <summary>Markdown body after front matter, trimmed.</summary>
    public string PromptTemplate { get; set; } = string.Empty;
}
