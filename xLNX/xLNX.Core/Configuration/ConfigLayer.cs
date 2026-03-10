using xLNX.Core.Models;

namespace xLNX.Core.Configuration;

/// <summary>
/// Parses WorkflowDefinition.Config into typed ServiceConfig with defaults and env resolution.
/// See SPEC Sections 6.1–6.4.
/// </summary>
public static class ConfigLayer
{
    /// <summary>
    /// Builds a typed ServiceConfig from workflow front-matter config.
    /// </summary>
    public static ServiceConfig Build(Dictionary<string, object?> rawConfig)
    {
        var config = new ServiceConfig();

        // Tracker
        var tracker = GetSection(rawConfig, "tracker");
        if (tracker != null)
        {
            config.TrackerKind = GetString(tracker, "kind") ?? string.Empty;
            config.TrackerEndpoint = GetString(tracker, "endpoint")
                ?? (config.TrackerKind == "linear" ? "https://api.linear.app/graphql" : string.Empty);
            config.TrackerApiKey = ResolveEnvValue(GetString(tracker, "api_key") ?? string.Empty);
            config.TrackerProjectSlug = GetString(tracker, "project_slug") ?? string.Empty;
            config.ActiveStates = ParseStringList(tracker, "active_states") ?? config.ActiveStates;
            config.TerminalStates = ParseStringList(tracker, "terminal_states") ?? config.TerminalStates;
        }

        // Polling
        var polling = GetSection(rawConfig, "polling");
        if (polling != null)
        {
            config.PollIntervalMs = GetInt(polling, "interval_ms") ?? config.PollIntervalMs;
        }

        // Workspace
        var workspace = GetSection(rawConfig, "workspace");
        if (workspace != null)
        {
            var root = GetString(workspace, "root");
            if (!string.IsNullOrEmpty(root))
            {
                config.WorkspaceRoot = ExpandPath(root);
            }
        }

        // Hooks
        var hooks = GetSection(rawConfig, "hooks");
        if (hooks != null)
        {
            config.HookAfterCreate = GetString(hooks, "after_create");
            config.HookBeforeRun = GetString(hooks, "before_run");
            config.HookAfterRun = GetString(hooks, "after_run");
            config.HookBeforeRemove = GetString(hooks, "before_remove");
            var timeout = GetInt(hooks, "timeout_ms");
            if (timeout.HasValue && timeout.Value > 0)
            {
                config.HookTimeoutMs = timeout.Value;
            }
        }

        // Agent
        var agent = GetSection(rawConfig, "agent");
        if (agent != null)
        {
            config.MaxConcurrentAgents = GetInt(agent, "max_concurrent_agents") ?? config.MaxConcurrentAgents;
            config.MaxTurns = GetInt(agent, "max_turns") ?? config.MaxTurns;
            config.MaxRetryBackoffMs = GetInt(agent, "max_retry_backoff_ms") ?? config.MaxRetryBackoffMs;
            config.MaxConcurrentAgentsByState = ParseStateConcurrencyMap(agent);
        }

        // Codex
        var codex = GetSection(rawConfig, "codex");
        if (codex != null)
        {
            config.CodexCommand = GetString(codex, "command") ?? config.CodexCommand;
            config.CodexApprovalPolicy = GetString(codex, "approval_policy");
            config.CodexThreadSandbox = GetString(codex, "thread_sandbox");
            config.CodexTurnSandboxPolicy = GetString(codex, "turn_sandbox_policy");
            config.CodexTurnTimeoutMs = GetInt(codex, "turn_timeout_ms") ?? config.CodexTurnTimeoutMs;
            config.CodexReadTimeoutMs = GetInt(codex, "read_timeout_ms") ?? config.CodexReadTimeoutMs;
            config.CodexStallTimeoutMs = GetInt(codex, "stall_timeout_ms") ?? config.CodexStallTimeoutMs;
        }

        // Server (extension)
        var server = GetSection(rawConfig, "server");
        if (server != null)
        {
            config.ServerPort = GetInt(server, "port");
        }

        return config;
    }

    /// <summary>
    /// Validates configuration before dispatch. See SPEC Section 6.3.
    /// </summary>
    public static ConfigValidationResult Validate(ServiceConfig config)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(config.TrackerKind))
            errors.Add("tracker.kind is required");
        else if (config.TrackerKind != "linear")
            errors.Add($"Unsupported tracker.kind: {config.TrackerKind}");

        if (string.IsNullOrWhiteSpace(config.TrackerApiKey))
            errors.Add("tracker.api_key is required (check $VAR resolution)");

        if (config.TrackerKind == "linear" && string.IsNullOrWhiteSpace(config.TrackerProjectSlug))
            errors.Add("tracker.project_slug is required for linear tracker");

        if (string.IsNullOrWhiteSpace(config.CodexCommand))
            errors.Add("codex.command must be present and non-empty");

        return new ConfigValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    /// <summary>
    /// Resolves $VAR references in a value string.
    /// </summary>
    public static string ResolveEnvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        if (value.StartsWith('$'))
        {
            var varName = value[1..];
            var envValue = Environment.GetEnvironmentVariable(varName);
            return string.IsNullOrEmpty(envValue) ? string.Empty : envValue;
        }

        return value;
    }

    /// <summary>
    /// Expands ~ and $VAR in path values.
    /// </summary>
    public static string ExpandPath(string path)
    {
        if (path.StartsWith('~'))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            path = Path.Combine(home, path[1..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        if (path.StartsWith('$'))
        {
            path = ResolveEnvValue(path);
        }

        return path;
    }

    private static Dictionary<string, object?>? GetSection(Dictionary<string, object?> config, string key)
    {
        if (config.TryGetValue(key, out var value) && value is Dictionary<object, object?> dict)
        {
            return dict.ToDictionary(kv => kv.Key.ToString()!, kv => kv.Value);
        }
        return null;
    }

    private static string? GetString(Dictionary<string, object?> section, string key)
    {
        return section.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    private static int? GetInt(Dictionary<string, object?> section, string key)
    {
        if (section.TryGetValue(key, out var value) && value != null)
        {
            if (value is int i) return i;
            if (int.TryParse(value.ToString(), out var parsed)) return parsed;
        }
        return null;
    }

    private static List<string>? ParseStringList(Dictionary<string, object?> section, string key)
    {
        if (!section.TryGetValue(key, out var value) || value == null)
            return null;

        if (value is List<object> list)
        {
            return list.Select(x => x.ToString()!).ToList();
        }

        var str = value.ToString();
        if (str != null)
        {
            return str.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        return null;
    }

    private static Dictionary<string, int> ParseStateConcurrencyMap(Dictionary<string, object?> agentSection)
    {
        var result = new Dictionary<string, int>();

        if (agentSection.TryGetValue("max_concurrent_agents_by_state", out var value)
            && value is Dictionary<object, object?> map)
        {
            foreach (var kv in map)
            {
                var stateKey = kv.Key.ToString()?.Trim().ToLowerInvariant();
                if (stateKey != null && kv.Value != null && int.TryParse(kv.Value.ToString(), out var limit) && limit > 0)
                {
                    result[stateKey] = limit;
                }
            }
        }

        return result;
    }
}

/// <summary>
/// Result of configuration validation.
/// </summary>
public sealed class ConfigValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = [];
}
