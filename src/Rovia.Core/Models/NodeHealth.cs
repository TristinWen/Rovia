namespace Rovia.Core.Models;

/// <summary>Contains the latest observable quality metrics for a node.</summary>
public sealed record NodeHealth
{
    public required string   NodeId          { get; init; }
    public double?           LatencyMs       { get; init; }
    public double?           TcpHandshakeMs  { get; init; }
    public double?           JitterMs        { get; init; }
    public double?           PacketLoss      { get; init; }
    public double            SuccessRate     { get; init; }
    public int               ConsecutiveFailures { get; init; }
    public NodeHealthState   State           { get; init; } = NodeHealthState.Unknown;
    public DateTimeOffset    LastCheckedAt   { get; init; }
}
