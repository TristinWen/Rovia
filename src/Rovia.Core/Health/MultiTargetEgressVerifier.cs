using Rovia.Core.Abstractions;
using Rovia.Core.Models;

namespace Rovia.Core.Health;

/// <summary>Verifies a proxy route against independent endpoints to avoid provider-specific false failures.</summary>
public sealed class MultiTargetEgressVerifier(
    IEgressProbe probe,
    IEnumerable<Uri>? targets = null,
    int maximumAttempts = 2,
    TimeSpan? retryDelay = null)
{
    private readonly IReadOnlyList<Uri> _targets = targets?.ToArray() ??
        [new("https://www.google.com/generate_204"), new("https://cp.cloudflare.com/generate_204"), new("https://www.msftconnecttest.com/connecttest.txt")];
    private readonly TimeSpan _retryDelay = retryDelay ?? TimeSpan.FromMilliseconds(300);

    public async Task<EgressVerificationResult> VerifyAsync(Uri proxyEndpoint, CancellationToken cancellationToken = default)
    {
        if (maximumAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
        EgressVerificationResult? result = null;
        for (int attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            List<EgressProbeResult> results = [];
            foreach (Uri target in _targets)
                results.Add(await probe.ProbeAsync(proxyEndpoint, target, cancellationToken));
            result = new(results);
            bool retryable = results.All(item => item.FailureKind is EgressFailureKind.Connection or EgressFailureKind.Timeout);
            if (result.Success || !retryable || attempt == maximumAttempts)
                return result;
            await Task.Delay(_retryDelay, cancellationToken);
        }
        return result!;
    }
}
