namespace Rovia.Core.Models;

/// <summary>Describes protocol transport options independent of a backend schema.</summary>
public sealed record TransportOptions(
    string Type,
    string? Path = null,
    string? Host = null,
    string? ServiceName = null,
    string? Method = null,
    string? IdleTimeout = null,
    string? PingTimeout = null,
    IDictionary<string, string>? Headers = null);