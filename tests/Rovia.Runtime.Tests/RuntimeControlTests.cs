using Rovia.Runtime.Runtime;
using Rovia.Runtime.Diagnostics;

namespace Rovia.Runtime.Tests;

/// <summary>Validates named-pipe runtime commands.</summary>
public sealed class RuntimeControlTests
{
    [Fact]
    public async Task SendAsync_ReturnsStatusAndRequestsDisconnect()
    {
        string pipeName = $"rovia-tests-{Guid.NewGuid():N}";
        bool stopped    = false;
        using CancellationTokenSource cancellation = new();
        RuntimeControlServer server = new(pipeName, () => new RuntimeState { IsRunning = true, ProcessId = Environment.ProcessId },
            () => stopped = true, liveLogProvider: sequence => [new(sequence + 1, DateTimeOffset.UtcNow, "INFO", "live")]);
        Task serverTask = server.RunAsync(cancellation.Token);
        RuntimeControlClient client = new(pipeName);

        RuntimeState status = await client.SendAsync("status");
        IReadOnlyList<RuntimeLiveLogEntry> logs = await client.ReadLiveLogsAsync(4);
        RuntimeState disconnect = await client.SendAsync("disconnect");
        cancellation.Cancel();
        await serverTask;

        Assert.True(status.IsRunning);
        Assert.True(disconnect.IsRunning);
        Assert.True(stopped);
        Assert.Equal(5, logs.Single().Sequence);
    }
}
