using System.Net;
using Rovia.Core.Models;

namespace Rovia.Config.Parsing;

/// <summary>Parses VLESS share links without coupling them to a backend schema.</summary>
public sealed class VlessLinkParser : IProxyLinkParser
{
    private static readonly HashSet<string> KnownParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "security", "sni", "fp", "pbk", "sid", "flow", "type", "path", "host", "serviceName", "encryption",
        "method", "idleTimeout", "pingTimeout", "mode", "xPaddingFrom", "xPaddingTo", "scPostFrom", "scPostTo"
    };

    public bool CanParse(string input) => input.StartsWith("vless://", StringComparison.OrdinalIgnoreCase);

    public ProxyNode Parse(string input)
    {
        if (!CanParse(input) || !Uri.TryCreate(input, UriKind.Absolute, out Uri? uri))
            throw new ProxyLinkParseException("The value is not a valid VLESS share link.");

        string userId = Uri.UnescapeDataString(uri.UserInfo);
        if (!Guid.TryParse(userId, out _))
            throw new ProxyLinkParseException("The VLESS user identifier must be a UUID.");
        if (string.IsNullOrWhiteSpace(uri.Host) || uri.Port is < 1 or > 65535)
            throw new ProxyLinkParseException("The VLESS link must contain a valid host and port.");

        Dictionary<string, string> parameters = ParseQuery(uri.Query);
        string security                       = Get(parameters, "security") ?? "none";
        bool tlsEnabled                       = security.Equals("tls", StringComparison.OrdinalIgnoreCase)
                                                || security.Equals("reality", StringComparison.OrdinalIgnoreCase);
        Dictionary<string, string> metadata   = parameters
            .Where(pair => !KnownParameters.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        if (!security.Equals("none", StringComparison.OrdinalIgnoreCase))
            metadata["security"] = security;

        return new ProxyNode
        {
            Name        = Uri.UnescapeDataString(uri.Fragment.TrimStart('#')),
            Protocol    = ProxyProtocol.Vless,
            Host        = uri.Host,
            Port        = uri.Port,
            Credentials = new(userId),
            Flow        = Get(parameters, "flow"),
            Tls         = tlsEnabled ? new(true, Get(parameters, "sni"), Get(parameters, "fp"), Get(parameters, "pbk"), Get(parameters, "sid")) : null,
            Transport   = BuildTransport(parameters),
            Metadata    = metadata
        };
    }

    private static TransportOptions? BuildTransport(IReadOnlyDictionary<string, string> parameters)
    {
        string? type = Get(parameters, "type");
        if (string.IsNullOrWhiteSpace(type) || type.Equals("tcp", StringComparison.OrdinalIgnoreCase))
            return null;
        ByteRange? padding = ParseByteRange(Get(parameters, "xPaddingFrom"), Get(parameters, "xPaddingTo"));
        ByteRange? post    = ParseByteRange(Get(parameters, "scPostFrom"), Get(parameters, "scPostTo"));
        return new(type,
            Get(parameters, "path"),
            Get(parameters, "host"),
            Get(parameters, "serviceName"),
            Get(parameters, "method"),
            Get(parameters, "idleTimeout"),
            Get(parameters, "pingTimeout"),
            null,
            Get(parameters, "mode"),
            padding,
            post);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (string item in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] pair = item.Split('=', 2);
            result[WebUtility.UrlDecode(pair[0])] = pair.Length == 2 ? WebUtility.UrlDecode(pair[1]) : string.Empty;
        }

        return result;
    }

    private static string? Get(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static ByteRange? ParseByteRange(string? from, string? to)
    {
        if (int.TryParse(from, out int f) && int.TryParse(to, out int t))
            return new ByteRange(f, t);
        return null;
    }
}
