using Rovia.Core.Models;
using Rovia.Core.Policies;

namespace Rovia.Core.Routing;

/// <summary>Selects routes while applying configurable hysteresis and emergency failover.</summary>
public sealed class RouteSelector
{
    private string? _currentNodeId;
    private DateTimeOffset _selectedAt;
    private DateTimeOffset _lastSwitchAt;

    public RouteScore? Select(IReadOnlyList<RouteScore> scores, IReadOnlyDictionary<string, NodeHealth> health,
        RoutingPolicy policy, DateTimeOffset now)
    {
        RouteScore? best = scores.OrderByDescending(score => score.Value).ThenBy(score => score.NodeId, StringComparer.Ordinal).FirstOrDefault();
        if (best is null)
            return null;
        if (_currentNodeId is null)
            return Set(best, now);

        RouteScore? current = scores.FirstOrDefault(score => score.NodeId == _currentNodeId);
        bool failed = current is null || !health.TryGetValue(_currentNodeId, out NodeHealth? currentHealth)
                      || currentHealth.State == NodeHealthState.Offline
                      || currentHealth.ConsecutiveFailures >= policy.FailureThreshold;
        if (failed)
            return Set(best, now);
        if (best.NodeId == current!.NodeId)
            return current;
        if (now - _selectedAt < policy.MinimumRouteLifetime || now - _lastSwitchAt < policy.SwitchCooldown)
            return current;
        return best.Value - current.Value >= policy.MinimumImprovement ? Set(best, now) : current;
    }

    private RouteScore Set(RouteScore score, DateTimeOffset now)
    {
        if (_currentNodeId is not null && _currentNodeId != score.NodeId)
            _lastSwitchAt = now;
        _currentNodeId = score.NodeId;
        _selectedAt    = now;
        return score;
    }
}
