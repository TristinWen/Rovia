using Rovia.Core.Models;

namespace Rovia.Config.Subscriptions;

/// <summary>Summarizes a subscription import without exposing credentials.</summary>
public sealed record SubscriptionImportResult(IReadOnlyList<ProxyNode> Nodes, int DuplicateCount, IReadOnlyList<string> Warnings);
