using System.Net;
using Rovia.Core.Models;
using Rovia.Runtime.Diagnostics;

namespace Rovia.Runtime.Tests;

public sealed class ResolvedEndpointCandidateFactoryTests
{
    [Fact]
    public void Expand_PreservesDomainIdentityForResolvedWebSocketAddresses()
    {
        ProxyNode node = new()
        {
            Id        = "edge",
            Name      = "primary",
            Host      = "proxy.example.com",
            Port      = 443,
            Tls       = new(true),
            Transport = new("ws", "/proxy")
        };

        IReadOnlyList<ProxyNode> result = ResolvedEndpointCandidateFactory.Expand(
            node,
            [IPAddress.Parse("203.0.113.10"), IPAddress.Parse("2001:db8::10")]);

        Assert.Equal(3, result.Count);
        Assert.Equal("proxy.example.com", result[0].Host);
        Assert.Equal("203.0.113.10", result[1].Host);
        Assert.Equal("proxy.example.com", result[1].Tls?.ServerName);
        Assert.Equal("proxy.example.com", result[1].Transport?.Host);
        Assert.Equal("2001:db8::10", result[2].Host);
    }

    [Fact]
    public void Expand_DoesNotRewriteNonWebSocketTransport()
    {
        ProxyNode node = new() { Id = "grpc", Host = "proxy.example.com", Port = 443, Tls = new(true), Transport = new("grpc") };

        IReadOnlyList<ProxyNode> result = ResolvedEndpointCandidateFactory.Expand(node, [IPAddress.Loopback]);

        Assert.Same(node, Assert.Single(result));
    }
}
