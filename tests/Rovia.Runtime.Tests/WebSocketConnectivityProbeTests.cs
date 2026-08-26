using Rovia.Runtime.Diagnostics;

namespace Rovia.Runtime.Tests;

/// <summary>Validates deterministic WebSocket diagnostic target construction.</summary>
public sealed class WebSocketConnectivityProbeTests
{
    [Theory]
    [InlineData(true, "proxy", "wss", 443, "/proxy")]
    [InlineData(false, "/", "ws", 80, "/")]
    public void BuildUri_NormalizesSchemeAndPath(bool tls, string path, string scheme, int port, string expectedPath)
    {
        Uri target = WebSocketConnectivityProbe.BuildUri("example.com", port, tls, path);

        Assert.Equal(scheme, target.Scheme);
        Assert.Equal(port, target.Port);
        Assert.Equal(expectedPath, target.AbsolutePath);
    }
}
