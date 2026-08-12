namespace Rovia.Core.Policies;

/// <summary>Describes a language-neutral domain, IP, or process routing rule.</summary>
public sealed record RoutingRule
{
    public IReadOnlyList<string> Domains       { get; init; } = [];
    public IReadOnlyList<string> DomainSuffixes { get; init; } = [];
    public IReadOnlyList<string> IpCidrs       { get; init; } = [];
    public IReadOnlyList<string> ProcessNames  { get; init; } = [];
    public RouteAction           Action        { get; init; } = RouteAction.Proxy;
}
