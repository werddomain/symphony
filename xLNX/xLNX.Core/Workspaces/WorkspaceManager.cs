using Microsoft.Extensions.Logging;
using xLNX.Core.Models;

namespace xLNX.Core.Workspaces;

/// <summary>
/// Manages workspace creation, reuse, hooks, and cleanup.
/// See SPEC Sections 9.1–9.5.
/// </summary>
public class WorkspaceManager
{
    private readonly Func<ServiceConfig> _configProvider;
    private readonly ILogger<WorkspaceManager> _logger;

    public WorkspaceManager(Func<ServiceConfig> configProvider, ILogger<WorkspaceManager> logger)
    {
        _configProvider = configProvider;
        _logger = logger;
    }

    /// <summary>
    /// Sanitizes an issue identifier to a workspace-safe directory name.
    /// Only [A-Za-z0-9._-] are allowed; all other characters become '_'.
    /// </summary>
    public static string SanitizeIdentifier(string identifier)
    {
        return new string(identifier.Select(c =>
            char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-' ? c : '_'
        ).ToArray());
    }

    /// <summary>
    /// Creates or reuses a workspace for the given issue identifier.
    /// </summary>
    public async Task<Workspace> CreateForIssueAsync(string issueIdentifier, CancellationToken ct = default)
    {
        var config = _configProvider();
        var workspaceKey = SanitizeIdentifier(issueIdentifier);
        var workspacePath = Path.GetFullPath(Path.Combine(config.WorkspaceRoot, workspaceKey));

        // Safety: ensure workspace path is under workspace root
        var rootPath = Path.GetFullPath(config.WorkspaceRoot);
        if (!workspacePath.StartsWith(rootPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Workspace path {workspacePath} is outside root {rootPath}");
        }

        bool createdNow = false;
        if (!Directory.Exists(workspacePath))
        {
            Directory.CreateDirectory(workspacePath);
            createdNow = true;

            if (!string.IsNullOrEmpty(config.HookAfterCreate))
            {
                await RunHookAsync(config.HookAfterCreate, workspacePath, config.HookTimeoutMs, "after_create", ct);
            }
        }

        return new Workspace
        {
            Path = workspacePath,
            WorkspaceKey = workspaceKey,
            CreatedNow = createdNow
        };
    }

    /// <summary>
    /// Runs a hook script in the workspace directory.
    /// </summary>
    public async Task RunHookAsync(string script, string workspacePath, int timeoutMs, string hookName, CancellationToken ct = default)
    {
        _logger.LogInformation("Running hook {HookName} in {Workspace}", hookName, workspacePath);

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "bash",
            Arguments = $"-lc {EscapeShellArg(script)}",
            WorkingDirectory = workspacePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(psi);
        if (process == null)
        {
            throw new InvalidOperationException($"Failed to start hook {hookName}");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeoutMs);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"Hook {hookName} timed out after {timeoutMs}ms");
        }

        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException($"Hook {hookName} failed with exit code {process.ExitCode}: {stderr}");
        }
    }

    /// <summary>
    /// Removes a workspace directory for the given identifier.
    /// </summary>
    public async Task RemoveWorkspaceAsync(string issueIdentifier, CancellationToken ct = default)
    {
        var config = _configProvider();
        var workspaceKey = SanitizeIdentifier(issueIdentifier);
        var workspacePath = Path.GetFullPath(Path.Combine(config.WorkspaceRoot, workspaceKey));

        if (!Directory.Exists(workspacePath))
            return;

        if (!string.IsNullOrEmpty(config.HookBeforeRemove))
        {
            try
            {
                await RunHookAsync(config.HookBeforeRemove, workspacePath, config.HookTimeoutMs, "before_remove", ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "before_remove hook failed for {Workspace}, continuing cleanup", workspacePath);
            }
        }

        try
        {
            Directory.Delete(workspacePath, recursive: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete workspace {Workspace}", workspacePath);
        }
    }

    private static string EscapeShellArg(string arg)
    {
        return "'" + arg.Replace("'", "'\\''") + "'";
    }
}
