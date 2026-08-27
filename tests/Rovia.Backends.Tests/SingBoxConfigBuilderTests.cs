using System.Text.Json;
using System.Diagnostics;
using Rovia.Backends.SingBox;
using Rovia.Core.Models;
using Rovia.Core.Policies;

namespace Rovia.Backends.Tests;

/// <summary>Validates deterministic sing-box configuration generation.</summary>
public sealed class SingBoxConfigBuilderTests
{
    [Fact]
    public void Build_MapsVlessRealityAndWebSocket()
    {
        ProxyNode node = new()
        {
            Id = "node", Protocol = ProxyProtocol.Vless, Host = "example.com", Port = 443,
            Credentials = new("11111111-1111-1111-1111-111111111111"),
            Tls = new(true, "cdn.example.com", "chrome", "public-key", "ab"),
            Transport = new("ws", "/proxy", "edge.example.com")
        };

        string first  = new SingBoxConfigBuilder().Build(node, new SingBoxOptions());
        string second = new SingBoxConfigBuilder().Build(node, new SingBoxOptions());
        using JsonDocument document = JsonDocument.Parse(first);

        Assert.Equal(first, second);
        Assert.Equal("vless", document.RootElement.GetProperty("outbounds")[0].GetProperty("type").GetString());
        Assert.Equal("public-key", document.RootElement.GetProperty("outbounds")[0].GetProperty("tls").GetProperty("reality").GetProperty("public_key").GetString());
    }

    [Fact]
    public void Build_MapsHttpTransportWithHeaders()
    {
        ProxyNode node = new()
        {
            Id = "node", Protocol = ProxyProtocol.Vless, Host = "example.com", Port = 443,
            Credentials = new("11111111-1111-1111-1111-111111111111"),
            Tls = new(true, "example.com"),
            Transport = new("http", "/api/v1/data", "example.com", null, "POST", "30s", "10s",
                new Dictionary<string, string> { ["User-Agent"] = "Mozilla/5.0" })
        };

        string json = new SingBoxConfigBuilder().Build(node, new SingBoxOptions());
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement transport = document.RootElement.GetProperty("outbounds")[0].GetProperty("transport");

        Assert.Equal("http", transport.GetProperty("type").GetString());
        Assert.Equal("/api/v1/data", transport.GetProperty("path").GetString());
        Assert.Equal("example.com", transport.GetProperty("host")[0].GetString());
        Assert.Equal("POST", transport.GetProperty("method").GetString());
        Assert.Equal("30s", transport.GetProperty("idle_timeout").GetString());
        Assert.Equal("10s", transport.GetProperty("ping_timeout").GetString());
        Assert.Equal("Mozilla/5.0", transport.GetProperty("headers").GetProperty("User-Agent").GetString());
    }

    [Fact]
    public void Build_MapsH2Transport()
    {
        ProxyNode node = new()
        {
            Id = "node", Protocol = ProxyProtocol.Vless, Host = "example.com", Port = 443,
            Credentials = new("11111111-1111-1111-1111-111111111111"),
            Tls = new(true, "example.com"),
            Transport = new("h2", "/stream", "example.com")
        };

        string json = new SingBoxConfigBuilder().Build(node, new SingBoxOptions());
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement transport = document.RootElement.GetProperty("outbounds")[0].GetProperty("transport");

        Assert.Equal("h2", transport.GetProperty("type").GetString());
        Assert.Equal("/stream", transport.GetProperty("path").GetString());
        Assert.Equal("example.com", transport.GetProperty("host")[0].GetString());
    }

    [Fact]
    public void Build_MapsXhttpTransport()
    {
        ProxyNode node = new()
        {
            Id = "node", Protocol = ProxyProtocol.Vless, Host = "example.com", Port = 443,
            Credentials = new("11111111-1111-1111-1111-111111111111"),
            Tls = new(true, "example.com"),
            Transport = new("xhttp", "/api/v1/stream", "example.com", null, null, null, null, null,
                "auto", new ByteRange(100, 1000), new ByteRange(1_000_000, 1_000_000))
        };

        string json = new SingBoxConfigBuilder().Build(node, new SingBoxOptions());
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement transport = document.RootElement.GetProperty("outbounds")[0].GetProperty("transport");

        Assert.Equal("xhttp", transport.GetProperty("type").GetString());
        Assert.Equal("example.com", transport.GetProperty("host").GetString());
        Assert.Equal("/api/v1/stream", transport.GetProperty("path").GetString());
        Assert.Equal("auto", transport.GetProperty("mode").GetString());
        Assert.Equal(100,  transport.GetProperty("x_padding_bytes").GetProperty("from").GetInt32());
        Assert.Equal(1000, transport.GetProperty("x_padding_bytes").GetProperty("to").GetInt32());
        Assert.Equal(1_000_000, transport.GetProperty("sc_max_each_post_bytes").GetProperty("from").GetInt32());
        Assert.Equal(1_000_000, transport.GetProperty("sc_max_each_post_bytes").GetProperty("to").GetInt32());
    }

    [Fact]
    public void Build_AddsTunInboundOnlyWhenRequested()
    {
        ProxyNode node = new() { Protocol = ProxyProtocol.Vless, Host = "example.com", Port = 443, Credentials = new("11111111-1111-1111-1111-111111111111") };
        string json = new SingBoxConfigBuilder().Build(node, new SingBoxOptions { Mode = SingBoxConnectionMode.Tun });
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Contains(document.RootElement.GetProperty("inbounds").EnumerateArray(), inbound => inbound.GetProperty("type").GetString() == "tun");
    }

    [Fact]
    public void Build_MapsRoutingActionsAndLeakResistantDns()
    {
        ProxyNode node = new() { Protocol = ProxyProtocol.Vless, Host = "example.com", Port = 443, Credentials = new("11111111-1111-1111-1111-111111111111") };
        SingBoxOptions options = new()
        {
            RoutingRules =
            [
                new() { DomainSuffixes = ["internal.example"], Action = RouteAction.Direct },
                new() { Domains = ["tracker.example"], Action = RouteAction.Block },
                new() { ProcessNames = ["browser.exe"], Action = RouteAction.Proxy }
            ],
            DnsPolicy = new() { RemoteResolver = new("https://1.1.1.1/dns-query"), ProxyRemoteQueries = true }
        };

        using JsonDocument document = JsonDocument.Parse(new SingBoxConfigBuilder().Build(node, options));
        JsonElement root             = document.RootElement;
        JsonElement rules            = root.GetProperty("route").GetProperty("rules");

        Assert.Equal("direct", rules[0].GetProperty("outbound").GetString());
        Assert.Equal("reject", rules[1].GetProperty("action").GetString());
        Assert.Equal("proxy", rules[2].GetProperty("outbound").GetString());
        Assert.Equal("local", root.GetProperty("route").GetProperty("default_domain_resolver").GetString());
        Assert.Equal("proxy", root.GetProperty("dns").GetProperty("servers")[0].GetProperty("detour").GetString());
        Assert.False(root.GetProperty("dns").GetProperty("disable_cache").GetBoolean());
    }

    [Fact]
    public void Build_RejectsMatcherlessRoutingRule()
    {
        ProxyNode node = new() { Protocol = ProxyProtocol.Vless, Host = "example.com", Port = 443, Credentials = new("11111111-1111-1111-1111-111111111111") };
        SingBoxOptions options = new() { RoutingRules = [new()] };

        Assert.Throws<InvalidOperationException>(() => new SingBoxConfigBuilder().Build(node, options));
    }

    [Fact]
    public async Task Build_PassesInstalledSingBoxValidationWhenEnabled()
    {
        string? executable = Environment.GetEnvironmentVariable("ROVIA_SING_BOX_TEST_PATH");
        if (string.IsNullOrWhiteSpace(executable))
            return;
        string directory = Path.Combine(Path.GetTempPath(), $"rovia-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            ProxyNode node = new()
            {
                Protocol = ProxyProtocol.Vless, Host = "example.com", Port = 443,
                Credentials = new("11111111-1111-1111-1111-111111111111"), Tls = new(true, "example.com")
            };
            SingBoxOptions options = new()
            {
                RoutingRules =
                [
                    new() { DomainSuffixes = ["internal.example"], Action = RouteAction.Direct },
                    new() { Domains = ["tracker.example"], Action = RouteAction.Block }
                ]
            };
            string configPath = Path.Combine(directory, "sing-box.json");
            await File.WriteAllTextAsync(configPath, new SingBoxConfigBuilder().Build(node, options));
            ProcessStartInfo startInfo = new(executable) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
            startInfo.ArgumentList.Add("check");
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(configPath);
            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start sing-box validation.");
            string error          = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            Assert.True(process.ExitCode == 0, error);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
