using System.Text.Json;
using Rovia.Backends.SingBox;
using Rovia.Core.Models;

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
}
