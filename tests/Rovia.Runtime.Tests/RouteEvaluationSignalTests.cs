using Rovia.Runtime.Monitoring;

namespace Rovia.Runtime.Tests;

public sealed class RouteEvaluationSignalTests
{
    [Fact]
    public async Task Pulse_WakesWaiterBeforePeriodicDelay()
    {
        RouteEvaluationSignal signal = new();
        Task waiting                 = signal.WaitAsync(TimeSpan.FromSeconds(10));

        signal.Pulse();

        await waiting.WaitAsync(TimeSpan.FromSeconds(1));
    }
}
