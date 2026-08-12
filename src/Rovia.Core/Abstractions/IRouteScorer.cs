using Rovia.Core.Models;
using Rovia.Core.Policies;

namespace Rovia.Core.Abstractions;

/// <summary>Scores a node from backend-independent health evidence.</summary>
public interface IRouteScorer
{
    RouteScore Score(ProxyNode node, NodeHealth health, RoutingPolicy policy);
}
