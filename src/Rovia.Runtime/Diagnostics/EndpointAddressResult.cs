namespace Rovia.Runtime.Diagnostics;

/// <summary>Reports bounded TCP reachability for one resolved endpoint address.</summary>
public sealed record EndpointAddressResult(string Address, bool TcpConnected, double? TcpMilliseconds, string? Error);
