using Rovia.Core.Models;

namespace Rovia.Core.Routing;

/// <summary>Selects explicitly grouped transport alternatives without inventing server capabilities.</summary>
public static class FallbackCandidateSelector
{
    public const string GroupMetadataKey = "rovia-fallback-group";

    public static IReadOnlyList<ProxyNode> ForManualSelection(ProxyNode primary, IReadOnlyList<ProxyNode> nodes)
    {
        if (!primary.Metadata.TryGetValue(GroupMetadataKey, out string? group) || string.IsNullOrWhiteSpace(group))
            return [primary];

        return nodes
            .Where(node => node.Id == primary.Id ||
                           node.Metadata.TryGetValue(GroupMetadataKey, out string? candidateGroup) &&
                           candidateGroup.Equals(group, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(node => node.Id == primary.Id)
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .ToArray();
    }
}
