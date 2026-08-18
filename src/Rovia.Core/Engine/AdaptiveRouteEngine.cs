using Rovia.Core.Abstractions;
using Rovia.Core.Failover;
using Rovia.Core.Health;
using Rovia.Core.Models;
using Rovia.Core.Policies;
using Rovia.Core.Routing;

namespace Rovia.Core.Engine;

/// <summary>Coordinates node health, scoring, selection, failover, and backend switching.</summary>
public sealed class AdaptiveRouteEngine(
    INodeRepository repository,
    HealthMonitor healthMonitor,
    IRouteScorer scorer,
    RouteSelector selector,
    FailoverEngine failover,
    IProxyBackend backend,
    RoutingPolicy policy) : IAsyncDisposable
{
    private readonly Dictionary<string, NodeHealth> _health = new(StringComparer.Ordinal);

    public ProxyNode? CurrentNode { get; private set; }
    public FailoverState FailoverState => failover.State;
    public RouteSelectionDecision? LastSelectionDecision { get; private set; }
    public event EventHandler<RouteChangedEventArgs>? RouteChanged;

    public void AddNode(ProxyNode node) => repository.Add(node);
    public bool RemoveNode(string id) => repository.Remove(id);
    public IReadOnlyList<ProxyNode> GetNodes() => repository.GetAll();
    public IReadOnlyDictionary<string, NodeHealth> GetHealth() => _health;

    public async Task<NodeHealth> ProbeAsync(ProxyNode node, CancellationToken cancellationToken = default)
    {
        NodeHealth health = await healthMonitor.CheckAsync(node, cancellationToken);
        _health[node.Id] = health;
        if (CurrentNode?.Id == node.Id)
            failover.Update(health, policy);
        return health;
    }

    public async Task<IReadOnlyList<RouteScore>> RankAsync(CancellationToken cancellationToken = default)
    {
        foreach (ProxyNode node in repository.GetAll())
            await ProbeAsync(node, cancellationToken);
        return repository.GetAll()
            .Select(node => scorer.Score(node, _health[node.Id], policy))
            .OrderByDescending(score => score.Value)
            .ThenBy(score => score.NodeId, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<ProxyNode?> SelectBestRouteAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RouteScore> scores = await RankAsync(cancellationToken);
        LastSelectionDecision            = selector.Decide(scores, _health, policy, DateTimeOffset.UtcNow);
        RouteScore? selected             = LastSelectionDecision.Selected;
        return selected is null ? null : repository.Get(selected.NodeId);
    }

    public async Task ConnectAsync(ProxyNode node, CancellationToken cancellationToken = default)
    {
        ProxyNode? previous = CurrentNode;
        BackendStatus status = await backend.GetStatusAsync(cancellationToken);
        if (status.IsRunning)
            await backend.SwitchNodeAsync(node, cancellationToken);
        else
            await backend.StartAsync(node, cancellationToken);
        CurrentNode = node;
        RouteChanged?.Invoke(this, new(previous, node));
    }

    public async Task<ProxyNode> ConnectBestAsync(CancellationToken cancellationToken = default)
    {
        ProxyNode node = await SelectBestRouteAsync(cancellationToken)
            ?? throw new InvalidOperationException("No usable proxy node is available.");
        if (_health[node.Id].State == NodeHealthState.Offline)
            throw new InvalidOperationException("All proxy nodes are offline.");
        await ConnectAsync(node, cancellationToken);
        return node;
    }

    public Task<BackendStatus> GetStatusAsync(CancellationToken cancellationToken = default) => backend.GetStatusAsync(cancellationToken);

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await backend.StopAsync(cancellationToken);
        CurrentNode = null;
    }

    public async ValueTask DisposeAsync() => await backend.DisposeAsync();
}
