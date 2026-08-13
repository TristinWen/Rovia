using System.Text.Json;
using Rovia.Core.Abstractions;
using Rovia.Core.Models;

namespace Rovia.Config.Storage;

/// <summary>Persists proxy nodes in a local UTF-8 JSON document.</summary>
public sealed class JsonNodeRepository : INodeRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;
    private readonly ICredentialProtector? _protector;
    private readonly Dictionary<string, ProxyNode> _nodes;

    public JsonNodeRepository(string path, ICredentialProtector? protector = null)
    {
        _path      = Path.GetFullPath(path);
        _protector = protector;
        IReadOnlyList<ProxyNode> storedNodes = Load(_path);
        bool requiresMigration = protector is not null && storedNodes.Any(HasUnprotectedCredentials);
        _nodes = storedNodes.Select(Unprotect).ToDictionary(node => node.Id, StringComparer.Ordinal);
        if (requiresMigration)
            Save();
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
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(_nodes.Values.Select(Protect), JsonOptions));
        File.Move(temporaryPath, _path, true);
    }

    private bool HasUnprotectedCredentials(ProxyNode node) => node.Credentials is { } credentials &&
        (!_protector!.IsProtected(credentials.Username) ||
         credentials.Password is { Length: > 0 } password && !_protector.IsProtected(password));

    private ProxyNode Protect(ProxyNode node) => TransformCredentials(node, _protector is null ? null : _protector.Protect);
    private ProxyNode Unprotect(ProxyNode node) => TransformCredentials(node, _protector is null ? null : _protector.Unprotect);

    private static ProxyNode TransformCredentials(ProxyNode node, Func<string, string>? transform)
    {
        if (transform is null || node.Credentials is not { } credentials)
            return node;
        return node with
        {
            Credentials = new ProxyCredentials(
                transform(credentials.Username),
                credentials.Password is null ? null : transform(credentials.Password))
        };
    }
}
