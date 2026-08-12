using Rovia.Core.Models;

namespace Rovia.Core.Abstractions;

/// <summary>Measures the current reachability and connection quality of a node.</summary>
public interface INodeProbe
{
    Task<NodeHealth> ProbeAsync(ProxyNode node, CancellationToken cancellationToken = default);
}
