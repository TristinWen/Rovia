using Rovia.Core.Models;
using Rovia.Runtime.Runtime;

namespace Rovia.Runtime.Tests;

public sealed class RuntimeLifetimeSupervisorTests
{
    [Fact]
    public async Task RunAsync_ReportsRepeatedBackendExit()
    {
        string? message = null;
        RuntimeLifetimeSupervisor supervisor = new(
            _ => Task.FromResult(new BackendStatus(false, null, null, null, "failure")),
            TimeSpan.FromMilliseconds(1));

        await supervisor.RunAsync(value => message = value);

        Assert.Contains("failure", message);
    }

    [Fact]
    public async Task RunAsync_ToleratesTransientStoppedSample()
    {
        Queue<bool> states = new([false, true, false, true]);
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(20));
        RuntimeLifetimeSupervisor supervisor = new(
            _ => Task.FromResult(new BackendStatus(states.Count == 0 || states.Dequeue(), null, null, null, null)),
            TimeSpan.FromMilliseconds(1));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            supervisor.RunAsync(_ => throw new Xunit.Sdk.XunitException("Transient exit was reported."), cancellation.Token));
    }
}
