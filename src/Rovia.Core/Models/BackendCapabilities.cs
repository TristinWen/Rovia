namespace Rovia.Core.Models;

/// <summary>Advertises optional features supported by a backend implementation.</summary>
public sealed record BackendCapabilities(
    bool SupportsTun,
    bool SupportsUdp,
    bool SupportsReality,
    bool SupportsHotSwitch,
    IReadOnlySet<ProxyProtocol> Protocols);
