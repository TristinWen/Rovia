namespace Rovia.Core.Models;

/// <summary>Summarizes independent proxy egress targets without relying on one provider.</summary>
public sealed record EgressVerificationResult(IReadOnlyList<EgressProbeResult> Results)
{
    public bool Success => Results.Any(result => result.Success);
    public EgressProbeResult? FastestSuccess => Results.Where(result => result.Success).MinBy(result => result.Duration);
    public string Message => Success
        ? $"Proxy egress verified through {Results.Count(result => result.Success)} of {Results.Count} independent targets."
        : $"Proxy egress failed for all targets: {string.Join("; ", Results.Select(result => $"{result.Target.Host}={result.FailureKind}"))}";
}
