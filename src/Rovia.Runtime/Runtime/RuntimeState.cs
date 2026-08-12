using Rovia.Core.Models;

namespace Rovia.Runtime.Runtime;

/// <summary>Provides a serializable snapshot of the long-running routing host.</summary>
public sealed record RuntimeState
{
    public bool              IsRunning       { get; init; }
    public int               ProcessId       { get; init; }
    public string?           NodeId          { get; init; }
    public string?           NodeName        { get; init; }
    public string?           LocalEndpoint   { get; init; }
    public FailoverState     FailoverState   { get; init; }
    public DateTimeOffset    StartedAt       { get; init; }
    public DateTimeOffset    UpdatedAt       { get; init; }
    public string?           LastMessage     { get; init; }
    public double?           ProxyLatencyMs  { get; init; }
    public double?           DownloadMbps    { get; init; }
    public DateTimeOffset?   PerformanceAt   { get; init; }
}
