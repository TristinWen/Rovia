namespace Rovia.Runtime.Diagnostics;

/// <summary>Result of an HTTP transport connectivity probe.</summary>
public sealed record HttpConnectivityResult(
    Uri Target,
    bool Success,
    double ElapsedMs,
    int? StatusCode,
    string? ErrorMessage);