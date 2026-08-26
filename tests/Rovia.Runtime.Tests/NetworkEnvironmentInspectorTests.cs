using Rovia.Runtime.Diagnostics;

namespace Rovia.Runtime.Tests;

/// <summary>Validates deterministic Cloudflare WARP adapter classification.</summary>
public sealed class NetworkEnvironmentInspectorTests
{
    [Theory]
    [InlineData("Cloudflare WARP", "WireGuard Tunnel")]
    [InlineData("Ethernet", "Cloudflare WARP Adapter")]
    public void IsCloudflareWarp_DetectsKnownAdapterIdentity(string name, string description) =>
        Assert.True(NetworkEnvironmentInspector.IsCloudflareWarp(name, description));

    [Fact]
    public void IsCloudflareWarp_DoesNotMisclassifyOrdinaryEthernet() =>
        Assert.False(NetworkEnvironmentInspector.IsCloudflareWarp("Ethernet", "Intel Ethernet Controller"));
}
