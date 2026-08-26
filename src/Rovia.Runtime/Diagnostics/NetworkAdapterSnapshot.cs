namespace Rovia.Runtime.Diagnostics;

/// <summary>Describes non-sensitive addressing and routing evidence for one active adapter.</summary>
public sealed record NetworkAdapterSnapshot(
    string Name,
    string Description,
    string Status,
    string InterfaceType,
    bool IsCloudflareWarp,
    int? Mtu,
    IReadOnlyList<string> Addresses,
    IReadOnlyList<string> DnsServers,
    IReadOnlyList<string> Gateways);
