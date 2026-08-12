using System.Text.Json;
using Rovia.Core.Abstractions;
using Rovia.Core.Models;

namespace Rovia.Config.Storage;

/// <summary>Persists proxy nodes in a local UTF-8 JSON document.</summary>
public sealed class JsonNodeRepository : INodeRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;
    private readonly Dictionary<string, ProxyNode> _nodes;

    public JsonNodeRepository(string path)
    {
        _path  = Path.GetFullPath(path);
        _nodes = Load(_path).ToDictionary(node => node.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<ProxyNode> GetAll() => [.. _nodes.Values];
    public ProxyNode? Get(string id) => _nodes.GetValueOrDefault(id);

    public void Add(ProxyNode node)
    {
        _nodes[node.Id] = node;
        Save();
    }

    public bool Remove(string id)
    {
        bool removed = _nodes.Remove(id);
        if (removed)
            Save();
        return removed;
    }

    private static IReadOnlyList<ProxyNode> Load(string path) => File.Exists(path)
        ? JsonSerializer.Deserialize<List<ProxyNode>>(File.ReadAllText(path), JsonOptions) ?? []
        : [];

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        string temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(_nodes.Values, JsonOptions));
        File.Move(temporaryPath, _path, true);
    }
}
