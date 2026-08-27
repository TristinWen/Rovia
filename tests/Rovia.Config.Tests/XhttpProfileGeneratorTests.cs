using Rovia.Config.Export;
using Rovia.Config.Parsing;
using Rovia.Core.Models;

namespace Rovia.Config.Tests;

/// <summary>Validates importable XHTTP stability profiles.</summary>
public sealed class XhttpProfileGeneratorTests
{
    [Fact]
    public void Generate_ReturnsThreeRoundTrippableProfiles()
    {
        ProxyNode source = new()
        {
            Protocol    = ProxyProtocol.Vless,
            Host        = "edge.example.com",
            Port        = 443,
            Credentials = new("11111111-1111-1111-1111-111111111111"),
            Tls         = new(true, "tunnel.example.com", "chrome"),
            Transport   = new("xhttp", "/tunnel", "tunnel.example.com", XhttpMode: "stream-one")
        };

        IReadOnlyDictionary<string, string> profiles = new XhttpProfileGenerator().Generate(source);
        ProxyNode longStream = new VlessLinkParser().Parse(profiles["XHTTP-LongStream"]);
        ProxyNode fallback   = new VlessLinkParser().Parse(profiles["WS-Fallback"]);

        Assert.Equal(3, profiles.Count);
        Assert.Equal(6, longStream.Transport?.XhttpXmux?.MaxConcurrency);
        Assert.Equal(15, longStream.Transport?.XhttpXmux?.HKeepAlivePeriod);
        Assert.Equal("ws", fallback.Transport?.Type);
    }
}
