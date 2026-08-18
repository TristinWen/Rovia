using Rovia.Core.Models;

namespace Rovia.Runtime.Runtime;

/// <summary>Stops a runtime host after repeated unexpected backend exits.</summary>
public sealed class RuntimeLifetimeSupervisor(
    Func<CancellationToken, Task<BackendStatus>> statusProvider,
    TimeSpan interval,
    int failureThreshold = 2)
{
    public async Task RunAsync(Action<string> backendExited, CancellationToken cancellationToken = default)
    {
        int consecutiveFailures = 0;
        using PeriodicTimer timer = new(interval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            BackendStatus status = await statusProvider(cancellationToken);
            consecutiveFailures  = status.IsRunning ? 0 : consecutiveFailures + 1;
            if (consecutiveFailures < failureThreshold)
                continue;
            backendExited(string.IsNullOrWhiteSpace(status.Error)
                ? "Proxy backend exited unexpectedly."
                : $"Proxy backend exited unexpectedly: {status.Error}");
            return;
        }
    }
}
