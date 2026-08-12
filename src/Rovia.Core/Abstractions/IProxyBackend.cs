using Rovia.Core.Models;

namespace Rovia.Core.Abstractions;

/// <summary>Controls an external proxy transport backend.</summary>
public interface IProxyBackend : IAsyncDisposable
{
    Task StartAsync(ProxyNode node, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task SwitchNodeAsync(ProxyNode node, CancellationToken cancellationToken = default);
    Task<BackendStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}
