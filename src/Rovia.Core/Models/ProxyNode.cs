namespace Rovia.Core.Models;

/// <summary>Represents a backend-independent proxy route.</summary>
public sealed record ProxyNode
{
    public string                                Id          { get; init; } = Guid.NewGuid().ToString("N");
    public string                                Name        { get; init; } = string.Empty;
    public ProxyProtocol                         Protocol    { get; init; }
    public string                                Host        { get; init; } = string.Empty;
    public int                                   Port        { get; init; }
    public ProxyCredentials                      Credentials { get; init; } = new(string.Empty);
    public TlsOptions?                           Tls         { get; init; }
    public TransportOptions?                     Transport   { get; init; }
    public string?                               Flow        { get; init; }
    public IReadOnlyDictionary<string, string>   Metadata    { get; init; } = new Dictionary<string, string>();
}
