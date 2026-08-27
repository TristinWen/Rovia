using Rovia.Core.Abstractions;
using Rovia.Core.Health;
using Rovia.Core.Models;

namespace Rovia.Core.Tests;

/// <summary>Validates provider-independent proxy activation checks.</summary>
public sealed class MultiTargetEgressVerifierTests
{
    [Fact]
    public async Task VerifyAsync_AcceptsAnyIndependentSuccess()
    {
        Uri blocked = new("https://blocked.example/");
        Uri allowed = new("https://allowed.example/");
        EgressVerificationResult result = await new MultiTargetEgressVerifier(new StubProbe(allowed), [blocked, allowed])
            .VerifyAsync(new("http://127.0.0.1:2080"));

        Assert.True(result.Success);
        Assert.Equal(allowed, result.FastestSuccess?.Target);
        Assert.Contains("1 of 2", result.Message);
    }

    [Fact]
    public async Task VerifyAsync_ReportsEveryFailureWhenNoneSucceed()
    {
        Uri first  = new("https://first.example/");
        Uri second = new("https://second.example/");
        EgressVerificationResult result = await new MultiTargetEgressVerifier(new StubProbe(null), [first, second])
            .VerifyAsync(new("http://127.0.0.1:2080"));

        Assert.False(result.Success);
        Assert.Contains("first.example=Connection", result.Message);
        Assert.Contains("second.example=Connection", result.Message);
    }

    [Fact]
    public async Task VerifyAsync_RetriesTransientStartupFailures()
    {
        Uri target = new("https://eventual.example/");
        DelayedSuccessProbe probe = new();

        EgressVerificationResult result = await new MultiTargetEgressVerifier(
            probe, [target], maximumAttempts: 2, retryDelay: TimeSpan.Zero).VerifyAsync(new("http://127.0.0.1:2080"));

        Assert.True(result.Success);
        Assert.Equal(2, probe.Calls);
    }

    private sealed class StubProbe(Uri? success) : IEgressProbe
    {
        public Task<EgressProbeResult> ProbeAsync(Uri proxyEndpoint, Uri target, CancellationToken cancellationToken = default) =>
            Task.FromResult(new EgressProbeResult(target == success, target, TimeSpan.FromMilliseconds(target == success ? 5 : 10),
                target == success ? 204 : null, target == success ? EgressFailureKind.None : EgressFailureKind.Connection, "test"));
    }

    private sealed class DelayedSuccessProbe : IEgressProbe
    {
        public int Calls { get; private set; }

        public Task<EgressProbeResult> ProbeAsync(Uri proxyEndpoint, Uri target, CancellationToken cancellationToken = default)
        {
            Calls++;
            bool success = Calls > 1;
            return Task.FromResult(new EgressProbeResult(success, target, TimeSpan.FromMilliseconds(5), success ? 204 : null,
                success ? EgressFailureKind.None : EgressFailureKind.Connection, "test"));
        }
    }
}
