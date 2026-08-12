using Rovia.Core.Models;
using Rovia.Core.Policies;

namespace Rovia.Core.Failover;

/// <summary>Maintains an explicit failover state from current route health.</summary>
public sealed class FailoverEngine
{
    public FailoverState State { get; private set; } = FailoverState.Healthy;

    public FailoverState Update(NodeHealth health, RoutingPolicy policy)
    {
        State = health.State switch
        {
            NodeHealthState.Healthy when State is FailoverState.Failover or FailoverState.Unhealthy => FailoverState.Recovering,
            NodeHealthState.Healthy                                                        => FailoverState.Healthy,
            NodeHealthState.Degraded                                                       => FailoverState.Degraded,
            NodeHealthState.Offline                                                        => FailoverState.Failover,
            _ when health.ConsecutiveFailures >= policy.FailureThreshold                  => FailoverState.Failover,
            _                                                                              => FailoverState.Unhealthy
        };
        return State;
    }
}
