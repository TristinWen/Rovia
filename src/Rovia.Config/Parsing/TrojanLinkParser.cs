using Rovia.Core.Models;

namespace Rovia.Config.Parsing;

/// <summary>Parses Trojan share links into backend-independent nodes.</summary>
public sealed class TrojanLinkParser : IProxyLinkParser
{
    public bool CanParse(string input) => input.StartsWith("trojan://", StringComparison.OrdinalIgnoreCase);

    public ProxyNode Parse(string input)
    {
        if (!CanParse(input) || !Uri.TryCreate(input, UriKind.Absolute, out Uri? uri) || string.IsNullOrWhiteSpace(uri.UserInfo) || uri.Port < 1)
            throw new ProxyLinkParseException("The value is not a valid Trojan share link.");
        Dictionary<string, string> query = LinkQuery.Parse(uri.Query);
        string security = query.GetValueOrDefault("security") ?? "tls";
        return new()
        {
            Name = Uri.UnescapeDataString(uri.Fragment.TrimStart('#')), Protocol = ProxyProtocol.Trojan,
            Host = uri.Host, Port = uri.Port, Credentials = new(string.Empty, Uri.UnescapeDataString(uri.UserInfo)),
            Tls = security.Equals("none", StringComparison.OrdinalIgnoreCase) ? null : new(true, query.GetValueOrDefault("sni"), query.GetValueOrDefault("fp")),
            Transport = LinkQuery.Transport(query), Metadata = query
        };
    }
}
