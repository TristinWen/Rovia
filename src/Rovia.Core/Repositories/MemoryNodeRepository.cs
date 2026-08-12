using Rovia.Core.Abstractions;
using Rovia.Core.Models;

namespace Rovia.Core.Repositories;

/// <summary>Stores proxy nodes in process memory.</summary>
public sealed class MemoryNodeRepository : INodeRepository
{
    private readonly Dictionary<string, ProxyNode> _nodes = new(StringComparer.Ordinal);

    public IReadOnlyList<ProxyNode> GetAll() => [.. _nodes.Values];

    public ProxyNode? Get(string id) => _nodes.GetValueOrDefault(id);

    public void Add(ProxyNode node) => _nodes[node.Id] = node;

    public bool Remove(string id) => _nodes.Remove(id);
}
