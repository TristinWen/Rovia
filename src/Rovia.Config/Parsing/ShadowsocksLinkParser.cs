using System.Text;
using Rovia.Core.Models;

namespace Rovia.Config.Parsing;

/// <summary>Parses SIP002 Shadowsocks share links into backend-independent nodes.</summary>
public sealed class ShadowsocksLinkParser : IProxyLinkParser
{
    public bool CanParse(string input) => input.StartsWith("ss://", StringComparison.OrdinalIgnoreCase);

    public ProxyNode Parse(string input)
    {
        if (!CanParse(input))
            throw new ProxyLinkParseException("The value is not a Shadowsocks share link.");
        string value = input[5..];
        string[] fragment = value.Split('#', 2);
        string name       = fragment.Length == 2 ? Uri.UnescapeDataString(fragment[1]) : string.Empty;
        string authority  = fragment[0].Split('?', 2)[0];
        int at            = authority.LastIndexOf('@');
        string credentials;
        string endpoint;
        if (at >= 0)
        {
            credentials = Decode(authority[..at]);
            endpoint    = authority[(at + 1)..];
        }
        else
        {
            string decoded = Decode(authority);
            at = decoded.LastIndexOf('@');
            if (at < 0)
                throw new ProxyLinkParseException("The Shadowsocks link does not contain an endpoint.");
            credentials = decoded[..at];
            endpoint    = decoded[(at + 1)..];
        }
        int colon = endpoint.LastIndexOf(':');
        int separator = credentials.IndexOf(':');
        if (colon < 1 || separator < 1 || !int.TryParse(endpoint[(colon + 1)..], out int port))
            throw new ProxyLinkParseException("The Shadowsocks link contains invalid credentials or port.");
        return new()
        {
            Name = name, Protocol = ProxyProtocol.Shadowsocks, Host = endpoint[..colon].Trim('[', ']'), Port = port,
            Credentials = new(credentials[..separator], credentials[(separator + 1)..]),
            Metadata = new Dictionary<string, string> { ["method"] = credentials[..separator] }
        };
    }

    private static string Decode(string value)
    {
        try
        {
            string normalized = Uri.UnescapeDataString(value).Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight((normalized.Length + 3) / 4 * 4, '=');
            return Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
        }
        catch (FormatException)
        {
            return Uri.UnescapeDataString(value);
        }
    }
}
