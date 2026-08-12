namespace Rovia.Core.Models;

/// <summary>Describes the routing engine failover lifecycle.</summary>
public enum FailoverState
{
    Healthy,
    Degraded,
    Unhealthy,
    Failover,
    Recovering
}
