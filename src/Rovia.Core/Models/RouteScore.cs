namespace Rovia.Core.Models;

/// <summary>Contains a normalized deterministic route score.</summary>
public sealed record RouteScore
{
    public RouteScore(string nodeId, double value, string reason)
    {
        NodeId = nodeId;
        Value  = Math.Clamp(value, 0, 100);
        Reason = reason;
    }

    public string NodeId { get; }
    public double Value  { get; }
    public string Reason { get; }
}
