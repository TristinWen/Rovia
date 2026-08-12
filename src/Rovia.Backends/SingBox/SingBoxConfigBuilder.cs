using System.Text.Json;
using System.Text.Json.Nodes;
using Rovia.Core.Models;

namespace Rovia.Backends.SingBox;

/// <summary>Converts unified proxy nodes into deterministic sing-box configuration.</summary>
public sealed class SingBoxConfigBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string Build(ProxyNode node, SingBoxOptions options)
    {
        if (node.Protocol != ProxyProtocol.Vless)
            throw new NotSupportedException($"The sing-box adapter does not yet support {node.Protocol}.");

        JsonObject outbound = new()
        {
            ["type"]    = "vless",
            ["tag"]     = "proxy",
            ["server"]  = node.Host,
            ["server_port"] = node.Port,
            ["uuid"]    = node.Credentials.Username
        };
        Add(outbound, "flow", node.Flow);
        if (node.Tls is not null)
            outbound["tls"] = BuildTls(node);
        if (node.Transport is not null)
            outbound["transport"] = BuildTransport(node.Transport);

        JsonObject root = new()
        {
            ["log"] = new JsonObject { ["level"] = "info", ["timestamp"] = true },
            ["inbounds"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "mixed", ["tag"] = "mixed-in", ["listen"] = options.ListenAddress,
                    ["listen_port"] = options.ListenPort
                }
            },
            ["outbounds"] = new JsonArray { outbound, new JsonObject { ["type"] = "direct", ["tag"] = "direct" } },
            ["route"] = new JsonObject { ["final"] = "proxy" }
        };
        return root.ToJsonString(JsonOptions);
    }

    private static JsonObject BuildTls(ProxyNode node)
    {
        TlsOptions tls = node.Tls!;
        JsonObject result = new() { ["enabled"] = tls.Enabled };
        Add(result, "server_name", tls.ServerName);
        if (!string.IsNullOrWhiteSpace(tls.Fingerprint))
            result["utls"] = new JsonObject { ["enabled"] = true, ["fingerprint"] = tls.Fingerprint };
        if (!string.IsNullOrWhiteSpace(tls.RealityPublicKey))
            result["reality"] = new JsonObject { ["enabled"] = true, ["public_key"] = tls.RealityPublicKey, ["short_id"] = tls.RealityShortId ?? string.Empty };
        return result;
    }

    private static JsonObject BuildTransport(TransportOptions transport)
    {
        JsonObject result = new() { ["type"] = transport.Type };
        Add(result, "path", transport.Path);
        if (!string.IsNullOrWhiteSpace(transport.Host))
            result["headers"] = new JsonObject { ["Host"] = transport.Host };
        Add(result, "service_name", transport.ServiceName);
        return result;
    }

    private static void Add(JsonObject target, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            target[name] = value;
    }
}
