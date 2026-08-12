namespace Rovia.Core.Models;

/// <summary>Describes the observed availability of a proxy node.</summary>
public enum NodeHealthState
{
    Unknown,
    Healthy,
    Degraded,
    Unhealthy,
    Offline
}
