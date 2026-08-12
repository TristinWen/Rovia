using System.IO.Pipes;
using System.Text.Json;

namespace Rovia.Runtime.Runtime;

/// <summary>Sends commands to a running Rovia host through a local named pipe.</summary>
public sealed class RuntimeControlClient(string pipeName)
{
    public async Task<RuntimeState> SendAsync(string command, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout ?? TimeSpan.FromSeconds(3));
        await using NamedPipeClientStream pipe = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(timeoutSource.Token);
        await using StreamWriter writer = new(pipe, leaveOpen: true) { AutoFlush = true };
        using StreamReader reader = new(pipe, leaveOpen: true);
        await writer.WriteLineAsync(command.AsMemory(), timeoutSource.Token);
        string response = await reader.ReadLineAsync(timeoutSource.Token) ?? throw new IOException("Runtime returned no response.");
        return JsonSerializer.Deserialize<RuntimeState>(response) ?? throw new IOException("Runtime returned an invalid response.");
    }
}
