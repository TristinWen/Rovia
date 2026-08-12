using System.Net;
using Rovia.Core.Health;
using Rovia.Core.Models;

namespace Rovia.Core.Tests;

/// <summary>Validates end-to-end probe result classification.</summary>
public sealed class HttpEgressProbeTests
{
    [Fact]
    public async Task ProbeAsync_AcceptsSuccessfulTargetResponse()
    {
        HttpEgressProbe probe = new(TimeSpan.FromSeconds(1), _ => new StubHandler(new HttpResponseMessage(HttpStatusCode.NoContent)));

        EgressProbeResult result = await probe.ProbeAsync(new("http://127.0.0.1:2080"), new("https://example.com/generate_204"));

        Assert.True(result.Success);
        Assert.Equal(204, result.StatusCode);
        Assert.Equal(EgressFailureKind.None, result.FailureKind);
    }

    [Fact]
    public async Task ProbeAsync_ClassifiesAuthenticationResponse()
    {
        HttpEgressProbe probe = new(TimeSpan.FromSeconds(1), _ => new StubHandler(new HttpResponseMessage(HttpStatusCode.ProxyAuthenticationRequired)));

        EgressProbeResult result = await probe.ProbeAsync(new("http://127.0.0.1:2080"), new("https://example.com"));

        Assert.False(result.Success);
        Assert.Equal(EgressFailureKind.Authentication, result.FailureKind);
    }

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(response);
    }
}
