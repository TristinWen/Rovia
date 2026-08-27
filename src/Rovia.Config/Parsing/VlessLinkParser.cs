using System.Net;
using System.Text.Json;
using Rovia.Core.Models;

namespace Rovia.Config.Parsing;

/// <summary>Parses VLESS share links without coupling them to a backend schema.</summary>
public sealed class VlessLinkParser : IProxyLinkParser
{
    private static readonly HashSet<string> KnownParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "security", "sni", "fp", "pbk", "sid", "flow", "type", "path", "host", "serviceName", "encryption",
        "method", "idleTimeout", "pingTimeout", "mode", "extra", "xPaddingFrom", "xPaddingTo", "scPostFrom", "scPostTo"
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
        ParsedXhttpExtra extra = ParseXhttpExtra(Get(parameters, "extra"));
        return new(type,
            Get(parameters, "path"),
            Get(parameters, "host"),
            Get(parameters, "serviceName"),
            Get(parameters, "method"),
            Get(parameters, "idleTimeout"),
            Get(parameters, "pingTimeout"),
            null,
            Get(parameters, "mode"),
            padding ?? extra.Padding,
            post ?? extra.PostSize,
            extra.Xmux);
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

    private static ParsedXhttpExtra ParseXhttpExtra(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new();
        try
        {
            using JsonDocument document = JsonDocument.Parse(value);
            JsonElement root             = document.RootElement;
            ByteRange? padding           = ReadRange(root, "xPaddingBytes");
            ByteRange? postSize          = ReadRange(root, "scMaxEachPostBytes");
            if (!root.TryGetProperty("xmux", out JsonElement xmux) || xmux.ValueKind != JsonValueKind.Object)
                return new(padding, postSize);
            return new(padding, postSize, new(
                ReadInt(xmux, "maxConcurrency"),
                ReadInt(xmux, "maxConnections"),
                ReadInt(xmux, "cMaxReuseTimes"),
                ReadInt(xmux, "hMaxRequestTimes"),
                ReadInt(xmux, "hMaxReusableSecs"),
                ReadInt(xmux, "hKeepAlivePeriod")));
        }
        catch (JsonException exception)
        {
            throw new ProxyLinkParseException($"The XHTTP extra parameter is not valid JSON: {exception.Message}");
        }
    }

    private static ByteRange? ReadRange(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement value))
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int exact))
            return new(exact, exact);
        if (value.ValueKind != JsonValueKind.String)
            return null;
        string[] parts = (value.GetString() ?? string.Empty).Split('-', 2);
        return int.TryParse(parts[0], out int from) && int.TryParse(parts[^1], out int to) ? new(from, to) : null;
    }

    private static int? ReadInt(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement value))
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
            return number;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number))
            return number;
        return null;
    }

    private sealed record ParsedXhttpExtra(ByteRange? Padding = null, ByteRange? PostSize = null, XhttpXmuxOptions? Xmux = null);
}
