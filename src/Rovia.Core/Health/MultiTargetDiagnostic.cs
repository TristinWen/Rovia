using System.Net;
using Rovia.Core.Abstractions;
using Rovia.Core.Models;

namespace Rovia.Core.Health;

/// <summary>Diagnoses DNS and proxy egress across independent targets to avoid single-provider false negatives.</summary>
public sealed class MultiTargetDiagnostic(IEgressProbe egressProbe, IEnumerable<Uri>? targets = null)
{
    private readonly IReadOnlyList<Uri> _targets = targets?.ToArray() ??
        [new("https://www.google.com/generate_204"), new("https://cp.cloudflare.com/generate_204"), new("https://www.msftconnecttest.com/connecttest.txt")];

    public async Task<IReadOnlyList<DiagnosticTargetResult>> RunAsync(Uri proxyEndpoint, CancellationToken cancellationToken = default)
    {
        List<DiagnosticTargetResult> results = [];
        foreach (Uri target in _targets)
        {
            string[] addresses;
            try { addresses = (await Dns.GetHostAddressesAsync(target.Host, cancellationToken)).Select(address => address.ToString()).ToArray(); }
            catch (Exception exception) when (exception is System.Net.Sockets.SocketException or OperationCanceledException) { addresses = []; }
            EgressProbeResult egress = await egressProbe.ProbeAsync(proxyEndpoint, target, cancellationToken);
            results.Add(new(target, addresses.Length > 0, addresses, egress));
        }
        return results;
    }
}
