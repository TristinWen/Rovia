using System.Text.Json;
using System.Text.Json.Nodes;
using Rovia.Core.Models;

namespace Rovia.Backends.Xray;

/// <summary>Builds deterministic Xray client configuration for VLESS XHTTP nodes.</summary>
public sealed class XrayConfigBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string Build(ProxyNode node, XrayOptions options)
    {
        if (node.Protocol != ProxyProtocol.Vless || node.Transport?.Type.Equals("xhttp", StringComparison.OrdinalIgnoreCase) != true)
            throw new NotSupportedException("The Xray adapter currently accepts only VLESS XHTTP nodes.");

        JsonObject streamSettings = new()
        {
            ["network"]       = "xhttp",
            ["security"]      = node.Tls is null ? "none" : node.Metadata.GetValueOrDefault("security") ?? "tls",
            ["xhttpSettings"] = BuildXhttp(node.Transport)
        };
        if (node.Tls is not null)
            streamSettings["tlsSettings"] = BuildTls(node.Tls);

        JsonObject outbound = new()
        {
            ["tag"]      = "proxy",
            ["protocol"] = "vless",
            ["settings"] = new JsonObject
            {
                ["vnext"] = new JsonArray(new JsonObject
                {
                    ["address"] = node.Host,
                    ["port"]    = node.Port,
                    ["users"]   = new JsonArray(new JsonObject
                    {
                        ["id"]         = node.Credentials.Username,
                        ["encryption"] = "none",
                        ["flow"]       = node.Flow ?? string.Empty
                    })
                })
            },
            ["streamSettings"] = streamSettings
        };

        JsonObject root = new()
        {
            ["log"] = new JsonObject { ["loglevel"] = options.LogLevel },
            ["inbounds"] = new JsonArray(new JsonObject
            {
                ["tag"]      = "http-in",
                ["listen"]   = options.ListenAddress,
                ["port"]     = options.ListenPort,
                ["protocol"] = "http",
                ["settings"] = new JsonObject()
            }),
            ["outbounds"] = new JsonArray(outbound, new JsonObject { ["tag"] = "direct", ["protocol"] = "freedom" })
        };
        return root.ToJsonString(JsonOptions);
    }

    private static JsonObject BuildXhttp(TransportOptions transport)
    {
        JsonObject result = new();
        Add(result, "host", transport.Host);
        Add(result, "path", transport.Path);
        Add(result, "mode", transport.XhttpMode);
        JsonObject extra = new();
        AddRange(extra, "xPaddingBytes", transport.XPaddingBytes);
        AddRange(extra, "scMaxEachPostBytes", transport.SCMaxEachPostBytes);
        if (transport.XhttpXmux is not null)
            extra["xmux"] = BuildXmux(transport.XhttpXmux);
        if (extra.Count > 0)
            result["extra"] = extra;
        return result;
    }

    private static JsonObject BuildXmux(XhttpXmuxOptions value)
    {
        JsonObject result = new();
        Add(result, "maxConcurrency", value.MaxConcurrency);
        Add(result, "maxConnections", value.MaxConnections);
        Add(result, "cMaxReuseTimes", value.CMaxReuseTimes);
        Add(result, "hMaxRequestTimes", value.HMaxRequestTimes);
        Add(result, "hMaxReusableSecs", value.HMaxReusableSecs);
        Add(result, "hKeepAlivePeriod", value.HKeepAlivePeriod);
        return result;
    }

    private static JsonObject BuildTls(TlsOptions tls)
    {
        JsonObject result = new();
        Add(result, "serverName", tls.ServerName);
        Add(result, "fingerprint", tls.Fingerprint);
        result["alpn"] = new JsonArray("h2");
        return result;
    }

    private static void AddRange(JsonObject target, string name, ByteRange? value)
    {
        if (value is not null)
            target[name] = value.From == value.To ? value.From.ToString() : $"{value.From}-{value.To}";
    }

    private static void Add(JsonObject target, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            target[name] = value;
    }

    private static void Add(JsonObject target, string name, int? value)
    {
        if (value is not null)
            target[name] = value;
    }
}
