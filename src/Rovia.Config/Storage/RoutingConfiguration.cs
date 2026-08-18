using Rovia.Core.Policies;

namespace Rovia.Config.Storage;

/// <summary>Groups backend-independent routing rules and DNS behavior.</summary>
public sealed record RoutingConfiguration
{
    public IReadOnlyList<RoutingRule> Rules { get; init; } = [];
    public DnsPolicy Dns { get; init; } = new();
}
