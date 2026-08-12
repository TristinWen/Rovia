namespace Rovia.Core.Models;

/// <summary>Describes the current lifecycle and endpoint of a proxy backend.</summary>
public sealed record BackendStatus(bool IsRunning, int? ProcessId, string? NodeId, Uri? LocalEndpoint, string? Error = null);
