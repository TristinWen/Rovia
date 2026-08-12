namespace Rovia.Core.Models;

/// <summary>Describes an end-to-end request made through a local proxy endpoint.</summary>
public sealed record EgressProbeResult(
    bool Success,
    Uri Target,
    TimeSpan Duration,
    int? StatusCode,
    EgressFailureKind FailureKind,
    string Message);
