using System.Net;
using System.Text.Json;
using Rovia.Core.Models;

namespace Rovia.Config.Export;

/// <summary>Generates explicit Default, LongStream, and WebSocket fallback VLESS profiles.</summary>
public sealed class XhttpProfileGenerator
{
    public IReadOnlyDictionary<string, string> Generate(ProxyNode node)
    {
        if (node.Protocol != ProxyProtocol.Vless || node.Transport?.Type.Equals("xhttp", StringComparison.OrdinalIgnoreCase) != true)
            throw new InvalidOperationException("Profiles can be generated only for a VLESS XHTTP node.");

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["XHTTP-Default"]    = Build(node, node.Transport with { XhttpXmux = null }, "XHTTP-Default"),
            ["XHTTP-LongStream"] = Build(node, node.Transport with
            {
                XhttpMode = "stream-one",
                XhttpXmux = new(6, null, 24, 150, 90, 15)
            }, "XHTTP-LongStream"),
            ["WS-Fallback"] = Build(node, node.Transport with
            {
                Type          = "ws",
                XhttpMode     = null,
                XPaddingBytes = null,
                SCMaxEachPostBytes = null,
                XhttpXmux     = null
            }, "WS-Fallback")
        };
    }

    private static string Build(ProxyNode node, TransportOptions transport, string name)
    {
        Dictionary<string, string> query = new(StringComparer.Ordinal)
        {
            ["security"] = node.Metadata.GetValueOrDefault("security") ?? (node.Tls is null ? "none" : "tls"),
            ["type"]     = transport.Type
        };
        Add(query, "sni", node.Tls?.ServerName);
        Add(query, "fp", node.Tls?.Fingerprint);
        Add(query, "pbk", node.Tls?.RealityPublicKey);
        Add(query, "sid", node.Tls?.RealityShortId);
        Add(query, "flow", node.Flow);
        Add(query, "host", transport.Host);
        Add(query, "path", transport.Path);
        Add(query, "mode", transport.XhttpMode);
        if (transport.Type.Equals("xhttp", StringComparison.OrdinalIgnoreCase))
        {
            string? extra = BuildExtra(transport);
            Add(query, "extra", extra);
        }
        foreach ((string key, string value) in node.Metadata.Where(pair => !query.ContainsKey(pair.Key) && !IsSecretKey(pair.Key)))
            query[key] = value;
        string queryString = string.Join('&', query.Select(pair => $"{WebUtility.UrlEncode(pair.Key)}={WebUtility.UrlEncode(pair.Value)}"));
        string host        = node.Host.Contains(':') && !node.Host.StartsWith('[') ? $"[{node.Host}]" : node.Host;
        return $"vless://{node.Credentials.Username}@{host}:{node.Port}?{queryString}#{Uri.EscapeDataString(name)}";
    }

    private static string? BuildExtra(TransportOptions transport)
    {
        Dictionary<string, object> extra = new(StringComparer.Ordinal);
        if (transport.XPaddingBytes is not null)
            extra["xPaddingBytes"] = Format(transport.XPaddingBytes);
        if (transport.SCMaxEachPostBytes is not null)
            extra["scMaxEachPostBytes"] = Format(transport.SCMaxEachPostBytes);
        if (transport.XhttpXmux is not null)
        {
            XhttpXmuxOptions value = transport.XhttpXmux;
            Dictionary<string, int> xmux = new();
            Add(xmux, "maxConcurrency", value.MaxConcurrency);
            Add(xmux, "maxConnections", value.MaxConnections);
            Add(xmux, "cMaxReuseTimes", value.CMaxReuseTimes);
            Add(xmux, "hMaxRequestTimes", value.HMaxRequestTimes);
            Add(xmux, "hMaxReusableSecs", value.HMaxReusableSecs);
            Add(xmux, "hKeepAlivePeriod", value.HKeepAlivePeriod);
            extra["xmux"] = xmux;
        }
        return extra.Count == 0 ? null : JsonSerializer.Serialize(extra);
    }

    private static string Format(ByteRange value) => value.From == value.To ? value.From.ToString() : $"{value.From}-{value.To}";
    private static bool IsSecretKey(string key) => key.Contains("token", StringComparison.OrdinalIgnoreCase) || key.Contains("password", StringComparison.OrdinalIgnoreCase);

    private static void Add(IDictionary<string, string> values, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            values[key] = value;
    }

    private static void Add(IDictionary<string, int> values, string key, int? value)
    {
        if (value is not null)
            values[key] = value.Value;
    }
}
