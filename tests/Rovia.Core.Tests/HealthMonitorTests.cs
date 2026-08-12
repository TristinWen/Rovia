using Rovia.Core.Abstractions;
using Rovia.Core.Health;
using Rovia.Core.Models;

namespace Rovia.Core.Tests;

/// <summary>Validates rolling node health aggregation.</summary>
public sealed class HealthMonitorTests
{
    [Fact]
    public async Task CheckAsync_TracksSuccessRateAndConsecutiveFailures()
    {
        Queue<NodeHealth> results = new([
            Health("node", NodeHealthState.Healthy, 40), Health("node", NodeHealthState.Offline), Health("node", NodeHealthState.Offline)]);
        HealthMonitor monitor = new(new StubProbe(results));
        ProxyNode node        = new() { Id = "node", Host = "example.com", Port = 443 };

        await monitor.CheckAsync(node);
        await monitor.CheckAsync(node);
        NodeHealth current = await monitor.CheckAsync(node);

        Assert.Equal(2, current.ConsecutiveFailures);
        Assert.Equal(1d / 3, current.SuccessRate, 3);
    }

    private static NodeHealth Health(string id, NodeHealthState state, double? latency = null) =>
        new() { NodeId = id, State = state, LatencyMs = latency, SuccessRate = state == NodeHealthState.Healthy ? 1 : 0 };

    private sealed class StubProbe(Queue<NodeHealth> results) : INodeProbe
    {
        public Task<NodeHealth> ProbeAsync(ProxyNode node, CancellationToken cancellationToken = default) => Task.FromResult(results.Dequeue());
    }
}
