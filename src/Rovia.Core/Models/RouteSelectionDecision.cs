namespace Rovia.Core.Models;

/// <summary>Explains a deterministic route selection or hysteresis decision.</summary>
public sealed record RouteSelectionDecision(RouteScore? Selected, bool Switched, string Reason);
