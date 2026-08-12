using Rovia.Core.Abstractions;
using Rovia.Core.Health;
using Rovia.Core.Models;

namespace Rovia.Core.Tests;

/// <summary>Validates independent multi-target proxy diagnostics.</summary>
public sealed class MultiTargetDiagnosticTests
{
    [Fact]
    public async Task RunAsync_PreservesSuccessAndFailurePerTarget()
    {
        Uri first = new("https://localhost/one");
        Uri second = new("https://localhost/two");
        IReadOnlyList<DiagnosticTargetResult> results = await new MultiTargetDiagnostic(new StubProbe(first), [first, second])
            .RunAsync(new("http://127.0.0.1:2080"));
        Assert.True(results[0].Egress.Success);
        Assert.False(results[1].Egress.Success);
    }

    private sealed class StubProbe(Uri success) : IEgressProbe
    {
        public Task<EgressProbeResult> ProbeAsync(Uri proxyEndpoint, Uri target, CancellationToken cancellationToken = default) =>
            Task.FromResult(new EgressProbeResult(target == success, target, TimeSpan.Zero, target == success ? 204 : 500,
                target == success ? EgressFailureKind.None : EgressFailureKind.Http, string.Empty));
    }
}
