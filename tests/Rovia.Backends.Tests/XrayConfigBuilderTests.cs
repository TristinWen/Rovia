using System.Text.Json;
using Rovia.Backends.Xray;
using Rovia.Core.Models;

namespace Rovia.Backends.Tests;

/// <summary>Validates Xray XHTTP configuration and connection-reuse controls.</summary>
public sealed class XrayConfigBuilderTests
{
    [Fact]
    public void Build_MapsLongStreamXmuxWithoutRawTcpKeepaliveBytes()
    {
        ProxyNode node = new()
        {
            Protocol    = ProxyProtocol.Vless,
            Host        = "edge.example.com",
            Port        = 443,
            Credentials = new("11111111-1111-1111-1111-111111111111"),
            Tls         = new(true, "tunnel.example.com", "chrome"),
            Transport   = new("xhttp", "/tunnel", "tunnel.example.com", XhttpMode: "stream-one",
                XPaddingBytes: new(100, 1000), XhttpXmux: new(6, null, 24, 150, 90, 15))
        };

        using JsonDocument document = JsonDocument.Parse(new XrayConfigBuilder().Build(node, new XrayOptions()));
        JsonElement stream           = document.RootElement.GetProperty("outbounds")[0].GetProperty("streamSettings");
        JsonElement settings         = stream.GetProperty("xhttpSettings");
        JsonElement xmux             = settings.GetProperty("extra").GetProperty("xmux");

        Assert.Equal("xhttp", stream.GetProperty("network").GetString());
        Assert.Equal("stream-one", settings.GetProperty("mode").GetString());
        Assert.Equal(6, xmux.GetProperty("maxConcurrency").GetInt32());
        Assert.Equal(24, xmux.GetProperty("cMaxReuseTimes").GetInt32());
        Assert.Equal(90, xmux.GetProperty("hMaxReusableSecs").GetInt32());
        Assert.Equal(15, xmux.GetProperty("hKeepAlivePeriod").GetInt32());
    }
}
