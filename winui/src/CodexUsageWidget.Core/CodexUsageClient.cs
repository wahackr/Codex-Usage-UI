using System.Diagnostics;
using System.Text.Json.Nodes;

namespace CodexUsageWidget.Core;

// JSON-RPC over stdio against `codex app-server`, mirroring
// src/codex-client.cjs: same initialize handshake, per-request timeout,
// and fail-all of pending requests when the process dies.
public sealed class CodexUsageClient : IDisposable
{
    private readonly string _command;
    private readonly TimeSpan _timeout;
    private readonly Dictionary<long, TaskCompletionSource<JsonNode?>> _pending = new();
    private Process? _process;
    private StreamWriter? _stdin;
    private Task? _handshake;
    private long _sequence;

    public CodexUsageClient(string? command = null, TimeSpan? timeout = null)
    {
        _command = command ?? Environment.GetEnvironmentVariable("CODEX_BIN") ?? "codex";
        _timeout = timeout ?? TimeSpan.FromSeconds(15);
    }

    public async Task ConnectAsync()
    {
        if (_process is { HasExited: false } && _handshake is not null)
        {
            await _handshake;
            return;
        }
        Start();
        _handshake = HandshakeAsync();
        await _handshake;
    }

    public async Task<JsonNode?> ReadRateLimitsAsync()
    {
        await ConnectAsync();
        return await RequestAsync("account/rateLimits/read", new JsonObject
        {
            ["excludeResetCreditDetails"] = true,
            ["supportsLunaReserve"] = false,
        });
    }

    public void Close()
    {
        var process = _process;
        _process = null;
        _stdin = null;
        _handshake = null;
        if (process is null) return;
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch { /* already gone */ }
        process.Dispose();
    }

    public void Dispose() => Close();

    private void Start()
    {
        var plan = CommandResolver.Resolve(_command, new[] { "app-server", "--stdio" });
        var startInfo = new ProcessStartInfo
        {
            FileName = plan.FileName,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in plan.Arguments) startInfo.ArgumentList.Add(argument);

        var process = new Process { StartInfo = startInfo };
        process.Start();
        _process = process;
        _stdin = process.StandardInput;
        _stdin.AutoFlush = true;
        process.OutputDataReceived += (_, eventArgs) => HandleLine(eventArgs.Data);
        process.ErrorDataReceived += (_, _) => { /* stderr is not part of the protocol */ };
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => FailAll(new InvalidOperationException(
            $"Codex app-server exited ({ExitCode(process)})"));
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    private static string ExitCode(Process process)
    {
        try { return process.ExitCode.ToString(); }
        catch { return "unknown"; }
    }

    private async Task HandshakeAsync()
    {
        await RequestAsync("initialize", new JsonObject
        {
            ["clientInfo"] = new JsonObject
            {
                ["name"] = "codex_usage_widget",
                ["title"] = "Codex Usage Widget",
                ["version"] = "0.1.0",
            },
            ["capabilities"] = new JsonObject { ["experimentalApi"] = true },
        });
        Send(new JsonObject { ["method"] = "initialized" });
    }

    private Task<JsonNode?> RequestAsync(string method, JsonNode? parameters)
    {
        var id = Interlocked.Increment(ref _sequence);
        var completion = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var message = new JsonObject { ["id"] = id, ["method"] = method };
        if (parameters is not null) message["params"] = parameters;
        lock (_pending) _pending[id] = completion;
        try
        {
            Send(message);
        }
        catch (Exception error)
        {
            lock (_pending) _pending.Remove(id);
            completion.TrySetException(error);
            return completion.Task;
        }
        _ = EnforceTimeoutAsync(id, completion, method);
        return completion.Task;
    }

    private async Task EnforceTimeoutAsync(long id, TaskCompletionSource<JsonNode?> completion, string method)
    {
        var winner = await Task.WhenAny(completion.Task, Task.Delay(_timeout));
        if (winner == completion.Task) return;
        lock (_pending) _pending.Remove(id);
        completion.TrySetException(new TimeoutException($"Codex timed out while calling {method}"));
    }

    private void Send(JsonObject message)
    {
        var stdin = _stdin;
        if (_process is not { HasExited: false } || stdin is null)
            throw new InvalidOperationException("Codex app-server is not available");
        stdin.Write(message.ToJsonString());
        stdin.Write('\n');
    }

    private void HandleLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        JsonNode? message;
        try
        {
            message = JsonNode.Parse(line);
            if (message is not JsonObject) return;
            if (!message.AsObject().TryGetPropertyValue("id", out var idNode)) return;
            if (!long.TryParse(idNode?.ToString(), out var id)) return;
            TaskCompletionSource<JsonNode?>? pending;
            lock (_pending)
            {
                if (!_pending.TryGetValue(id, out pending)) return;
                _pending.Remove(id);
            }
            var error = message["error"];
            if (error is not null)
            {
                var text = error["message"]?.ToString();
                pending.TrySetException(new InvalidOperationException(
                    string.IsNullOrEmpty(text) ? "Codex request failed" : text));
            }
            else
            {
                pending.TrySetResult(message["result"]);
            }
        }
        catch
        {
            // Malformed or non-object lines are ignored, like the Node client.
        }
    }

    private void FailAll(Exception error)
    {
        List<TaskCompletionSource<JsonNode?>> pending;
        lock (_pending)
        {
            pending = _pending.Values.ToList();
            _pending.Clear();
        }
        foreach (var completion in pending) completion.TrySetException(error);
    }
}
