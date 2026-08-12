using Rovia.Core.Abstractions;
using Rovia.Core.Models;
using Rovia.Core.Policies;

namespace Rovia.Core.Routing;

/// <summary>Calculates deterministic scores with stability weighted above small latency gains.</summary>
public sealed class RouteScorer : IRouteScorer
{
    public RouteScore Score(ProxyNode node, NodeHealth health, RoutingPolicy policy)
    {
        if (health.State == NodeHealthState.Offline)
            return new(node.Id, 0, "offline");

        double availability = health.State switch
        {
            NodeHealthState.Healthy   => 25,
            NodeHealthState.Degraded  => 15,
            NodeHealthState.Unhealthy => 5,
            _                         => 10
        };
        double stability = 40 * Math.Clamp(health.SuccessRate, 0, 1);
        double latency   = 20 * Quality(health.LatencyMs, policy.LatencyTargetMs);
        double handshake = 10 * Quality(health.TcpHandshakeMs, policy.HandshakeTargetMs);
        double jitter    = 5 * Quality(health.JitterMs, 100);
        double penalty   = Math.Min(20, health.ConsecutiveFailures * 7);
        double value     = availability + stability + latency + handshake + jitter - penalty;
        return new(node.Id, value, $"availability={availability:0.#}, stability={stability:0.#}, latency={latency:0.#}, failures=-{penalty:0.#}");
    }

    private static double Quality(double? value, double target) => value.HasValue
        ? Math.Clamp(1 - value.Value / target, 0, 1)
        : 0.5;
}
