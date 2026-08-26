using Rovia.Core.Abstractions;
using Rovia.Core.Models;

namespace Rovia.Core.Health;

/// <summary>Verifies a proxy route against independent endpoints to avoid provider-specific false failures.</summary>
public sealed class MultiTargetEgressVerifier(IEgressProbe probe, IEnumerable<Uri>? targets = null)
{
    private readonly IReadOnlyList<Uri> _targets = targets?.ToArray() ??
        [new("https://www.google.com/generate_204"), new("https://cp.cloudflare.com/generate_204"), new("https://www.msftconnecttest.com/connecttest.txt")];

    public async Task<EgressVerificationResult> VerifyAsync(Uri proxyEndpoint, CancellationToken cancellationToken = default)
    {
        List<EgressProbeResult> results = [];
        foreach (Uri target in _targets)
            results.Add(await probe.ProbeAsync(proxyEndpoint, target, cancellationToken));
        return new(results);
    }
}
