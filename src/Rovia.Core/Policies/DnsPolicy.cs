namespace Rovia.Core.Policies;

/// <summary>Describes resolver and leak-prevention behavior independent of a backend.</summary>
public sealed record DnsPolicy
{
    public Uri RemoteResolver       { get; init; } = new("https://1.1.1.1/dns-query");
    public string LocalResolver     { get; init; } = "local";
    public bool ProxyRemoteQueries  { get; init; } = true;
    public bool PreferIpv6          { get; init; }
    public bool EnableCache         { get; init; } = true;
}
