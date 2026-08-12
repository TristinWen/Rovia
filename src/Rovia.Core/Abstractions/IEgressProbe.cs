using Rovia.Core.Models;

namespace Rovia.Core.Abstractions;

/// <summary>Verifies that application traffic can exit through a local proxy endpoint.</summary>
public interface IEgressProbe
{
    Task<EgressProbeResult> ProbeAsync(Uri proxyEndpoint, Uri target, CancellationToken cancellationToken = default);
}
