using Rovia.Core.Models;
using Rovia.Core.Policies;
using Rovia.Core.Routing;

namespace Rovia.Core.Tests;

/// <summary>Validates scoring stability, deterministic ranking, hysteresis, and failover.</summary>
public sealed class RoutingTests
{
    private static readonly RoutingPolicy Policy = new() { MinimumRouteLifetime = TimeSpan.Zero, SwitchCooldown = TimeSpan.Zero };

    [Fact]
    public void Score_StableSlowerNodeBeatsUnstableFastNode()
    {
        RouteScorer scorer = new();
        ProxyNode fast     = Node("fast");
        ProxyNode stable   = Node("stable");

        RouteScore fastScore   = scorer.Score(fast, Health("fast", 40, 0.4, 2), Policy);
        RouteScore stableScore = scorer.Score(stable, Health("stable", 55, 1), Policy);

        Assert.True(stableScore.Value > fastScore.Value);
    }

    [Fact]
    public void Select_PreventsSmallOptimizationButFailsOverImmediately()
    {
        RouteSelector selector = new();
        DateTimeOffset now     = DateTimeOffset.UtcNow;
        Dictionary<string, NodeHealth> health = new() { ["a"] = Health("a", 50, 1), ["b"] = Health("b", 40, 1) };

        Assert.Equal("a", selector.Select([new("a", 90, ""), new("b", 89, "")], health, Policy, now)?.NodeId);
        Assert.Equal("a", selector.Select([new("a", 90, ""), new("b", 91, "")], health, Policy, now.AddMinutes(1))?.NodeId);
        health["a"] = Health("a", null, 0, 3, NodeHealthState.Offline);
        Assert.Equal("b", selector.Select([new("a", 0, ""), new("b", 91, "")], health, Policy with { SwitchCooldown = TimeSpan.FromHours(1) }, now.AddMinutes(2))?.NodeId);
    }

    [Fact]
    public void Select_BreaksEqualScoresByNodeId()
    {
        RouteSelector selector = new();
        Dictionary<string, NodeHealth> health = new() { ["a"] = Health("a", 50, 1), ["b"] = Health("b", 50, 1) };
        Assert.Equal("a", selector.Select([new("b", 80, ""), new("a", 80, "")], health, Policy, DateTimeOffset.UtcNow)?.NodeId);
    }

    [Fact]
    public void Decide_ExplainsHysteresisAndEmergencyFailover()
    {
        RouteSelector selector = new();
        DateTimeOffset now     = DateTimeOffset.UtcNow;
        Dictionary<string, NodeHealth> health = new() { ["a"] = Health("a", 50, 1), ["b"] = Health("b", 40, 1) };
        selector.Decide([new("a", 90, "stable"), new("b", 89, "fast")], health, Policy, now);

        RouteSelectionDecision held = selector.Decide([new("a", 90, "stable"), new("b", 94, "fast")], health, Policy, now.AddMinutes(1));
        Assert.False(held.Switched);
        Assert.Contains("required 8.0", held.Reason);

        health["a"] = Health("a", null, 0, 3, NodeHealthState.Offline);
        RouteSelectionDecision failed = selector.Decide([new("a", 0, "offline"), new("b", 94, "healthy")], health, Policy, now.AddMinutes(2));
        Assert.True(failed.Switched);
        Assert.Contains("3 consecutive failures", failed.Reason);
    }

    private static ProxyNode Node(string id) => new() { Id = id, Host = "example.com", Port = 443 };
    private static NodeHealth Health(string id, double? latency, double success, int failures = 0, NodeHealthState state = NodeHealthState.Healthy) =>
        new() { NodeId = id, LatencyMs = latency, TcpHandshakeMs = latency, SuccessRate = success, ConsecutiveFailures = failures, State = state };
}
