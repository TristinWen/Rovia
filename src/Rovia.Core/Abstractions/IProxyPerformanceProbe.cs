using Rovia.Core.Models;

namespace Rovia.Core.Abstractions;

/// <summary>Measures application-visible latency and download throughput through a proxy.</summary>
public interface IProxyPerformanceProbe
{
    Task<ProxyPerformance> MeasureAsync(Uri proxyEndpoint, CancellationToken cancellationToken = default);
}
