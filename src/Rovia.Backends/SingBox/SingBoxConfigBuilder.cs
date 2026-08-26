using System.Text.Json;
using System.Text.Json.Nodes;
using Rovia.Core.Models;
using Rovia.Core.Policies;

namespace Rovia.Backends.SingBox;

/// <summary>Converts unified proxy nodes into deterministic sing-box configuration.</summary>
public sealed class SingBoxConfigBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string Build(ProxyNode node, SingBoxOptions options)
    {
        JsonObject outbound = node.Protocol switch
        {
            ProxyProtocol.Vless       => BuildUuidOutbound("vless", node),
            ProxyProtocol.Vmess       => BuildUuidOutbound("vmess", node),
            ProxyProtocol.Trojan      => BuildPasswordOutbound("trojan", node),
            ProxyProtocol.Shadowsocks => BuildShadowsocksOutbound(node),
            _ => throw new NotSupportedException($"The sing-box adapter does not yet support {node.Protocol}.")
        };
        Add(outbound, "flow", node.Flow);
        if (node.Tls is not null)
            outbound["tls"] = BuildTls(node);
        if (node.Transport is not null)
            outbound["transport"] = BuildTransport(node.Transport);

        JsonObject root = new()
        {
            ["log"] = new JsonObject { ["level"] = options.LogLevel, ["timestamp"] = true },
            ["inbounds"] = BuildInbounds(options),
            ["outbounds"] = new JsonArray { outbound, new JsonObject { ["type"] = "direct", ["tag"] = "direct" } },
            ["dns"] = BuildDns(options.DnsPolicy),
            ["route"] = BuildRoute(options.RoutingRules)
        };
        return root.ToJsonString(JsonOptions);
    }

    private static JsonObject BuildDns(DnsPolicy policy)
    {
        JsonObject remote = new()
        {
            ["type"]        = policy.RemoteResolver.Scheme == "https" ? "https" : policy.RemoteResolver.Scheme,
            ["tag"]         = "remote",
            ["server"]      = policy.RemoteResolver.Host
        };
        if (!policy.RemoteResolver.IsDefaultPort)
            remote["server_port"] = policy.RemoteResolver.Port;
        if (policy.RemoteResolver.Scheme == "https")
            remote["path"] = string.IsNullOrEmpty(policy.RemoteResolver.AbsolutePath) ? "/dns-query" : policy.RemoteResolver.AbsolutePath;
        if (policy.ProxyRemoteQueries)
            remote["detour"] = "proxy";
        JsonObject local = policy.LocalResolver == "local"
            ? new() { ["type"] = "local", ["tag"] = "local" }
            : new() { ["type"] = "udp", ["tag"] = "local", ["server"] = policy.LocalResolver };
        return new()
        {
            ["servers"]       = new JsonArray(remote, local),
            ["final"]         = "remote",
            ["strategy"]      = policy.PreferIpv6 ? "prefer_ipv6" : "prefer_ipv4",
            ["disable_cache"] = !policy.EnableCache
        };
    }

    private static JsonObject BuildRoute(IReadOnlyList<RoutingRule> rules)
    {
        JsonArray mapped = [];
        foreach (RoutingRule rule in rules)
        {
            if (rule.Domains.Count + rule.DomainSuffixes.Count + rule.IpCidrs.Count + rule.ProcessNames.Count == 0)
                throw new InvalidOperationException("A routing rule must contain at least one matcher.");
            JsonObject result = new();
            AddArray(result, "domain", rule.Domains);
            AddArray(result, "domain_suffix", rule.DomainSuffixes);
            AddArray(result, "ip_cidr", rule.IpCidrs);
            AddArray(result, "process_name", rule.ProcessNames);
            if (rule.Action == RouteAction.Block)
                result["action"] = "reject";
            else
            {
                result["action"]   = "route";
                result["outbound"] = rule.Action == RouteAction.Direct ? "direct" : "proxy";
            }
            mapped.Add(result);
        }
        return new() { ["rules"] = mapped, ["final"] = "proxy", ["default_domain_resolver"] = "local" };
    }

    private static void AddArray(JsonObject target, string name, IReadOnlyList<string> values)
    {
        if (values.Count > 0)
            target[name] = new JsonArray(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
    }

    private static JsonArray BuildInbounds(SingBoxOptions options)
    {
        JsonArray inbounds = new()
        {
            new JsonObject
            {
                ["type"] = "mixed", ["tag"] = "mixed-in", ["listen"] = options.ListenAddress,
                ["listen_port"] = options.ListenPort
            }
        };
        if (options.Mode == SingBoxConnectionMode.Tun)
            inbounds.Add(new JsonObject
            {
                ["type"] = "tun", ["tag"] = "tun-in", ["interface_name"] = "Rovia",
                ["address"] = new JsonArray("172.19.0.1/30"), ["mtu"] = 9000, ["auto_route"] = true,
                ["strict_route"] = true, ["stack"] = options.TunStack
            });
        return inbounds;
    }

    private static JsonObject BuildUuidOutbound(string type, ProxyNode node) => new()
    {
        ["type"] = type, ["tag"] = "proxy", ["server"] = node.Host, ["server_port"] = node.Port, ["uuid"] = node.Credentials.Username
    };

    private static JsonObject BuildPasswordOutbound(string type, ProxyNode node) => new()
    {
        ["type"] = type, ["tag"] = "proxy", ["server"] = node.Host, ["server_port"] = node.Port, ["password"] = node.Credentials.Password
    };

    private static JsonObject BuildShadowsocksOutbound(ProxyNode node) => new()
    {
        ["type"] = "shadowsocks", ["tag"] = "proxy", ["server"] = node.Host, ["server_port"] = node.Port,
        ["method"] = node.Metadata.GetValueOrDefault("method") ?? node.Credentials.Username, ["password"] = node.Credentials.Password
    };

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
        string type = transport.Type.ToLowerInvariant();
        JsonObject result = new() { ["type"] = type };
        switch (type)
        {
            case "http":
                Add(result, "path", transport.Path);
                if (!string.IsNullOrWhiteSpace(transport.Host))
                    result["host"] = new JsonArray(transport.Host);
                Add(result, "method", transport.Method);
                Add(result, "idle_timeout", transport.IdleTimeout);
                Add(result, "ping_timeout", transport.PingTimeout);
                if (transport.Headers is { Count: > 0 })
                {
                    JsonObject headers = new();
                    foreach (var pair in transport.Headers)
                        headers[pair.Key] = pair.Value;
                    result["headers"] = headers;
                }
                break;
            case "ws":
                Add(result, "path", transport.Path);
                if (!string.IsNullOrWhiteSpace(transport.Host))
                    result["headers"] = new JsonObject { ["Host"] = transport.Host };
                break;
            case "grpc":
                Add(result, "service_name", transport.ServiceName);
                Add(result, "idle_timeout", transport.IdleTimeout);
                Add(result, "ping_timeout", transport.PingTimeout);
                break;
            case "h2":
            case "httpupgrade":
                if (!string.IsNullOrWhiteSpace(transport.Host))
                    result["host"] = type == "h2" ? new JsonArray(transport.Host) : transport.Host;
                Add(result, "path", transport.Path);
                break;
            case "quic":
                break;
            default:
                throw new InvalidOperationException($"Unsupported transport type '{transport.Type}'.");
        }
        return result;
    }

    private static void Add(JsonObject target, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            target[name] = value;
    }
}