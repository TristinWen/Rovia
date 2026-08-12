namespace Rovia.Core.Policies;

/// <summary>Centralizes route scoring and switching thresholds.</summary>
public sealed record RoutingPolicy
{
    public double   MinimumImprovement    { get; init; } = 8;
    public TimeSpan MinimumRouteLifetime  { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan SwitchCooldown        { get; init; } = TimeSpan.FromSeconds(15);
    public int      FailureThreshold      { get; init; } = 3;
    public double   LatencyTargetMs       { get; init; } = 300;
    public double   HandshakeTargetMs     { get; init; } = 500;
}
