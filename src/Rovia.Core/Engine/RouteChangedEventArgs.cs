using Rovia.Core.Models;

namespace Rovia.Core.Engine;

/// <summary>Provides the previous and selected route when an engine route changes.</summary>
public sealed class RouteChangedEventArgs(ProxyNode? previous, ProxyNode current) : EventArgs
{
    public ProxyNode? Previous { get; } = previous;
    public ProxyNode Current   { get; } = current;
}
