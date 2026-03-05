namespace xLNX.Core.Models;

/// <summary>
/// Filesystem workspace assigned to one issue identifier.
/// See SPEC Section 4.1.4.
/// </summary>
public sealed class Workspace
{
    /// <summary>Absolute workspace path.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Sanitized issue identifier.</summary>
    public string WorkspaceKey { get; set; } = string.Empty;

    /// <summary>True only if the directory was created during this call.</summary>
    public bool CreatedNow { get; set; }
}
