using Rovia.Core.Models;

namespace Rovia.Core.Abstractions;

/// <summary>Stores and retrieves proxy nodes.</summary>
public interface INodeRepository
{
    IReadOnlyList<ProxyNode> GetAll();
    ProxyNode? Get(string id);
    void Add(ProxyNode node);
    bool Remove(string id);
}
