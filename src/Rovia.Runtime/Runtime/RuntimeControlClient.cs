using System.IO.Pipes;
using System.Text.Json;
using Rovia.Runtime.Diagnostics;

namespace Rovia.Runtime.Runtime;

/// <summary>Sends commands to a running Rovia host through a local named pipe.</summary>
public sealed class RuntimeControlClient(string pipeName)
{
    public async Task<RuntimeState> SendAsync(string command, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        string response = await SendRawAsync(command, timeout, cancellationToken);
        return JsonSerializer.Deserialize<RuntimeState>(response) ?? throw new IOException("Runtime returned an invalid response.");
    }

    public async Task<IReadOnlyList<RuntimeLiveLogEntry>> ReadLiveLogsAsync(
        long afterSequence,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        string response = await SendRawAsync($"live-logs {afterSequence}", timeout, cancellationToken);
        return JsonSerializer.Deserialize<IReadOnlyList<RuntimeLiveLogEntry>>(response)
               ?? throw new IOException("Runtime returned invalid live logs.");
    }

    private async Task<string> SendRawAsync(string command, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout ?? TimeSpan.FromSeconds(3));
        await using NamedPipeClientStream pipe = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(timeoutSource.Token);
        await using StreamWriter writer = new(pipe, leaveOpen: true) { AutoFlush = true };
        using StreamReader reader = new(pipe, leaveOpen: true);
        await writer.WriteLineAsync(command.AsMemory(), timeoutSource.Token);
        return await reader.ReadLineAsync(timeoutSource.Token) ?? throw new IOException("Runtime returned no response.");
    }
}
