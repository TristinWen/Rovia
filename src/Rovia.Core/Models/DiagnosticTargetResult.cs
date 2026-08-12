namespace Rovia.Core.Models;

/// <summary>Reports DNS and proxy egress evidence for one diagnostic target.</summary>
public sealed record DiagnosticTargetResult(Uri Target, bool DnsResolved, IReadOnlyList<string> Addresses, EgressProbeResult Egress);
