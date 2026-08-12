using Rovia.Core.Abstractions;
using Rovia.Core.Engine;
using Rovia.Core.Failover;
using Rovia.Core.Health;
using Rovia.Core.Models;
using Rovia.Core.Policies;
using Rovia.Core.Repositories;
using Rovia.Core.Routing;

namespace Rovia.Core.Tests;

/// <summary>Validates high-level engine orchestration without a real backend process.</summary>
public sealed class AdaptiveRouteEngineTests
{
    [Fact]
    public async Task ConnectBestAsync_ProbesRanksAndStartsBestNode()
    {
        MemoryNodeRepository repository = new();
        ProxyNode slower = new() { Id = "stable", Host = "stable", Port = 443 };
        ProxyNode failed = new() { Id = "failed", Host = "failed", Port = 443 };
        repository.Add(slower);
        repository.Add(failed);
        StubBackend backend = new();
        AdaptiveRouteEngine engine = new(repository, new HealthMonitor(new StubProbe()), new RouteScorer(),
            new RouteSelector(), new FailoverEngine(), backend, new RoutingPolicy());

        ProxyNode selected = await engine.ConnectBestAsync();

        Assert.Equal("stable", selected.Id);
        Assert.Equal("stable", backend.StartedNode?.Id);
    }

    private sealed class StubProbe : INodeProbe
    {
        public Task<NodeHealth> ProbeAsync(ProxyNode node, CancellationToken cancellationToken = default) => Task.FromResult(
            new NodeHealth { NodeId = node.Id, LatencyMs = 50, TcpHandshakeMs = 50, SuccessRate = node.Id == "stable" ? 1 : 0,
                State = node.Id == "stable" ? NodeHealthState.Healthy : NodeHealthState.Offline });
    }

    private sealed class StubBackend : IProxyBackend
    {
        public BackendCapabilities Capabilities { get; } = new(false, false, false, true, new HashSet<ProxyProtocol>());
        public ProxyNode? StartedNode { get; private set; }
        public Task StartAsync(ProxyNode node, CancellationToken cancellationToken = default) { StartedNode = node; return Task.CompletedTask; }
        public Task StopAsync(CancellationToken cancellationToken = default) { StartedNode = null; return Task.CompletedTask; }
        public Task SwitchNodeAsync(ProxyNode node, CancellationToken cancellationToken = default) { StartedNode = node; return Task.CompletedTask; }
        public Task<BackendStatus> GetStatusAsync(CancellationToken cancellationToken = default) => Task.FromResult(new BackendStatus(StartedNode is not null, null, StartedNode?.Id, null));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
