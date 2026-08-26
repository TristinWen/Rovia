using Rovia.Config.Parsing;
using Rovia.Core.Models;

namespace Rovia.Config.Tests;

/// <summary>Validates supported and malformed VLESS share links.</summary>
public sealed class VlessLinkParserTests
{
    private readonly VlessLinkParser _parser = new();

    [Fact]
    public void Parse_ReadsHttpTransportSettings()
    {
        ProxyNode node = _parser.Parse("vless://00000000-0000-0000-0000-000000000000@example.com:443?security=tls&type=http&host=edge.example.com&path=%2Ftunnel&method=POST&idleTimeout=30s&pingTimeout=10s#http");

        Assert.Equal("http", node.Transport?.Type);
        Assert.Equal("edge.example.com", node.Transport?.Host);
        Assert.Equal("/tunnel", node.Transport?.Path);
        Assert.Equal("POST", node.Transport?.Method);
        Assert.Equal("30s", node.Transport?.IdleTimeout);
        Assert.Equal("10s", node.Transport?.PingTimeout);
    }

    [Fact]
    public void Parse_MapsRealityWebSocketAndUnknownParameters()
    {
        ProxyNode node = _parser.Parse("vless://11111111-1111-1111-1111-111111111111@example.com:443?security=reality&sni=cdn.example.com&fp=chrome&pbk=key&sid=ab&type=ws&path=%2Fproxy&host=edge.example.com&custom=value#Tokyo%201");

        Assert.Equal("Tokyo 1", node.Name);
        Assert.Equal("example.com", node.Host);
        Assert.Equal("cdn.example.com", node.Tls?.ServerName);
        Assert.Equal("/proxy", node.Transport?.Path);
        Assert.Equal("value", node.Metadata["custom"]);
    }

    [Theory]
    [InlineData("not-a-link")]
    [InlineData("vless://invalid@example.com:443")]
    [InlineData("vless://11111111-1111-1111-1111-111111111111@example.com")]
    public void Parse_RejectsMalformedLinks(string input) => Assert.Throws<ProxyLinkParseException>(() => _parser.Parse(input));

    [Fact]
    public void Parse_SupportsIpv6Hosts()
    {
        ProxyNode node = _parser.Parse("vless://11111111-1111-1111-1111-111111111111@[2001:db8::1]:8443#IPv6");
        Assert.Equal("[2001:db8::1]", node.Host);
    }
}
