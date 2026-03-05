using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using xLNX.Core.Models;

namespace xLNX.Core.Agent;

/// <summary>
/// Wraps the coding-agent app-server subprocess client with JSON line protocol.
/// See SPEC Sections 10.1–10.7.
/// </summary>
public class AppServerClient : IDisposable
{
    private readonly ILogger<AppServerClient> _logger;
    private Process? _process;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private int _requestId;

    public AppServerClient(ILogger<AppServerClient> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Launches the coding-agent subprocess in the given workspace.
    /// </summary>
    public void Launch(string command, string workspacePath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "bash",
            Arguments = $"-lc {EscapeShellArg(command)}",
            WorkingDirectory = workspacePath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start coding agent process");

        _stdin = _process.StandardInput;
        _stdout = _process.StandardOutput;

        // Log stderr asynchronously
        _ = Task.Run(async () =>
        {
            while (!_process.HasExited)
            {
                var line = await _process.StandardError.ReadLineAsync();
                if (line != null)
                {
                    _logger.LogDebug("[codex stderr] {Line}", line);
                }
            }
        });
    }

    /// <summary>
    /// Performs the startup handshake: initialize, initialized, thread/start.
    /// See SPEC Section 10.2.
    /// </summary>
    public async Task<string> StartSessionAsync(string workspacePath, string? approvalPolicy, string? sandbox, int readTimeoutMs, CancellationToken ct)
    {
        // 1. initialize
        var initId = NextId();
        await SendAsync(new
        {
            id = initId,
            method = "initialize",
            @params = new
            {
                clientInfo = new { name = "symphony-xlnx", version = "1.0" },
                capabilities = new { }
            }
        });
        await ReadResponseAsync(initId, readTimeoutMs, ct);

        // 2. initialized notification
        await SendAsync(new { method = "initialized", @params = new { } });

        // 3. thread/start
        var threadId = NextId();
        var threadParams = new Dictionary<string, object?>
        {
            ["cwd"] = workspacePath
        };
        if (approvalPolicy != null) threadParams["approvalPolicy"] = approvalPolicy;
        if (sandbox != null) threadParams["sandbox"] = sandbox;

        await SendAsync(new { id = threadId, method = "thread/start", @params = threadParams });
        var threadResult = await ReadResponseAsync(threadId, readTimeoutMs, ct);

        // Extract thread_id from result.thread.id
        if (threadResult.TryGetProperty("result", out var result)
            && result.TryGetProperty("thread", out var thread)
            && thread.TryGetProperty("id", out var tid))
        {
            return tid.GetString() ?? throw new InvalidOperationException("thread/start returned null thread id");
        }

        throw new InvalidOperationException("thread/start response missing thread.id");
    }

    /// <summary>
    /// Starts a turn on the given thread.
    /// </summary>
    public async Task<string> StartTurnAsync(string threadId, string prompt, string workspacePath,
        string issueTitle, string? approvalPolicy, string? sandboxPolicy, int readTimeoutMs, CancellationToken ct)
    {
        var turnId = NextId();
        var turnParams = new Dictionary<string, object?>
        {
            ["threadId"] = threadId,
            ["input"] = new[] { new { type = "text", text = prompt } },
            ["cwd"] = workspacePath,
            ["title"] = issueTitle
        };
        if (approvalPolicy != null) turnParams["approvalPolicy"] = approvalPolicy;
        if (sandboxPolicy != null) turnParams["sandboxPolicy"] = new { type = sandboxPolicy };

        await SendAsync(new { id = turnId, method = "turn/start", @params = turnParams });
        var turnResult = await ReadResponseAsync(turnId, readTimeoutMs, ct);

        if (turnResult.TryGetProperty("result", out var result)
            && result.TryGetProperty("turn", out var turn)
            && turn.TryGetProperty("id", out var tid))
        {
            return tid.GetString() ?? throw new InvalidOperationException("turn/start returned null turn id");
        }

        throw new InvalidOperationException("turn/start response missing turn.id");
    }

    /// <summary>
    /// Reads streaming messages until turn completion.
    /// Returns the terminal event type.
    /// </summary>
    public async Task<string> StreamTurnAsync(int turnTimeoutMs, Action<JsonElement>? onMessage, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(turnTimeoutMs);

        while (!timeoutCts.Token.IsCancellationRequested)
        {
            var line = await ReadLineAsync(timeoutCts.Token);
            if (line == null) return "port_exit";

            JsonElement message;
            try
            {
                message = JsonSerializer.Deserialize<JsonElement>(line);
            }
            catch
            {
                continue; // Skip non-JSON lines
            }

            onMessage?.Invoke(message);

            if (message.TryGetProperty("method", out var method))
            {
                var methodStr = method.GetString();
                if (methodStr is "turn/completed" or "turn/failed" or "turn/cancelled")
                {
                    return methodStr;
                }
            }
        }

        return "turn_timeout";
    }

    public void Dispose()
    {
        try
        {
            _stdin?.Dispose();
            _process?.Kill(entireProcessTree: true);
            _process?.Dispose();
        }
        catch
        {
            // Best effort cleanup
        }
    }

    private async Task SendAsync(object message)
    {
        if (_stdin == null) throw new InvalidOperationException("Process not started");
        var json = JsonSerializer.Serialize(message);
        await _stdin.WriteLineAsync(json);
        await _stdin.FlushAsync();
    }

    private async Task<JsonElement> ReadResponseAsync(int expectedId, int timeoutMs, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeoutMs);

        while (!timeoutCts.Token.IsCancellationRequested)
        {
            var line = await ReadLineAsync(timeoutCts.Token);
            if (line == null) throw new InvalidOperationException("Process exited during handshake");

            try
            {
                var msg = JsonSerializer.Deserialize<JsonElement>(line);
                if (msg.TryGetProperty("id", out var id) && id.GetInt32() == expectedId)
                {
                    if (msg.TryGetProperty("error", out var error))
                    {
                        throw new InvalidOperationException($"Response error: {error}");
                    }
                    return msg;
                }
            }
            catch (JsonException)
            {
                // Skip non-JSON lines
            }
        }

        throw new TimeoutException($"Response timeout after {timeoutMs}ms");
    }

    private async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        if (_stdout == null) throw new InvalidOperationException("Process not started");
        try
        {
            return await _stdout.ReadLineAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private int NextId() => Interlocked.Increment(ref _requestId);

    private static string EscapeShellArg(string arg)
    {
        return "'" + arg.Replace("'", "'\\''") + "'";
    }
}
