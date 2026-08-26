namespace Rovia.Runtime.Diagnostics;

/// <summary>Reports whether a configured WebSocket transport completed its HTTP upgrade.</summary>
public sealed record WebSocketConnectivityResult(Uri Target, bool Success, double DurationMilliseconds, string? Error);
