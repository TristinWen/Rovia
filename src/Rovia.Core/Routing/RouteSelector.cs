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
        RoutingPolicy policy, DateTimeOffset now) => Decide(scores, health, policy, now).Selected;

    public RouteSelectionDecision Decide(IReadOnlyList<RouteScore> scores, IReadOnlyDictionary<string, NodeHealth> health,
        RoutingPolicy policy, DateTimeOffset now)
    {
        RouteScore? best = scores.OrderByDescending(score => score.Value).ThenBy(score => score.NodeId, StringComparer.Ordinal).FirstOrDefault();
        if (best is null)
            return new(null, false, "No route candidates are available.");
        if (_currentNodeId is null)
            return new(Set(best, now), true, $"Initial selection: {best.NodeId} scored {best.Value:0.0}. {best.Reason}");

        RouteScore? current = scores.FirstOrDefault(score => score.NodeId == _currentNodeId);
        if (current is null || !health.TryGetValue(_currentNodeId, out NodeHealth? currentHealth))
            return new(Set(best, now), true, $"Emergency switch: current route {_currentNodeId} has no health evidence; selected {best.NodeId} ({best.Value:0.0}).");
        if (currentHealth.State == NodeHealthState.Offline || currentHealth.ConsecutiveFailures >= policy.FailureThreshold)
            return new(Set(best, now), true, $"Emergency switch: {_currentNodeId} is {currentHealth.State} with {currentHealth.ConsecutiveFailures} consecutive failures; selected {best.NodeId} ({best.Value:0.0}).");
        if (best.NodeId == current!.NodeId)
            return new(current, false, $"Kept {current.NodeId}: it remains highest-ranked at {current.Value:0.0}.");
        if (now - _selectedAt < policy.MinimumRouteLifetime)
            return new(current, false, $"Kept {current.NodeId}: minimum route lifetime of {policy.MinimumRouteLifetime.TotalSeconds:0}s has not elapsed.");
        if (now - _lastSwitchAt < policy.SwitchCooldown)
            return new(current, false, $"Kept {current.NodeId}: switch cooldown of {policy.SwitchCooldown.TotalSeconds:0}s has not elapsed.");
        double improvement = best.Value - current.Value;
        return improvement >= policy.MinimumImprovement
            ? new(Set(best, now), true, $"Optimization switch: {best.NodeId} improves score by {improvement:0.0} (required {policy.MinimumImprovement:0.0}). {best.Reason}")
            : new(current, false, $"Kept {current.NodeId}: {best.NodeId} improves score by only {improvement:0.0} (required {policy.MinimumImprovement:0.0}).");
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
