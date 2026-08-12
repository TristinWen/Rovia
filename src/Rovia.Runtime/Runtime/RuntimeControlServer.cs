using System.IO.Pipes;
using System.Text.Json;

namespace Rovia.Runtime.Runtime;

/// <summary>Accepts status and disconnect requests for a long-running Rovia host.</summary>
public sealed class RuntimeControlServer(string pipeName, Func<RuntimeState> stateProvider, Action stopRequested)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using NamedPipeServerStream pipe = new(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            try { await pipe.WaitForConnectionAsync(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            using StreamReader reader = new(pipe, leaveOpen: true);
            await using StreamWriter writer = new(pipe, leaveOpen: true) { AutoFlush = true };
            string command = (await reader.ReadLineAsync(cancellationToken) ?? string.Empty).Trim().ToLowerInvariant();
            RuntimeState state = command is "status" or "disconnect"
                ? stateProvider()
                : stateProvider() with { LastMessage = $"Unknown runtime command '{command}'." };
            await writer.WriteLineAsync(JsonSerializer.Serialize(state).AsMemory(), cancellationToken);
            if (command == "disconnect")
                stopRequested();
        }
    }
}
