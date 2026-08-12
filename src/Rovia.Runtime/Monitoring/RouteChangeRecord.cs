namespace Rovia.Runtime.Monitoring;

/// <summary>Records why and when the runtime changed its active route.</summary>
public sealed record RouteChangeRecord(DateTimeOffset At, string? PreviousNodeId, string CurrentNodeId, string Reason);
