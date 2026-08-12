namespace Rovia.Core.Models;

/// <summary>Contains warmed proxy latency and bounded download throughput measurements.</summary>
public sealed record ProxyPerformance(
    double? LatencyMs,
    double? DownloadMbps,
    long DownloadedBytes,
    DateTimeOffset MeasuredAt,
    string Message);
