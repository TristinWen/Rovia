using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
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
    private readonly object _sync = new();

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

    public IReadOnlyList<ProxyNode> GetAll()
    {
        lock (_sync)
        {
            RefreshFromDisk();
            return [.. _nodes.Values];
        }
    }

    public ProxyNode? Get(string id)
    {
        lock (_sync)
        {
            RefreshFromDisk();
            return _nodes.GetValueOrDefault(id);
        }
    }

    public void Add(ProxyNode node)
    {
        ExecuteWrite(() =>
        {
            RefreshFromDisk();
            _nodes[node.Id] = node;
            Save();
            return true;
        });
    }

    public bool Remove(string id)
    {
        return ExecuteWrite(() =>
        {
            RefreshFromDisk();
            bool removed = _nodes.Remove(id);
            if (removed)
                Save();
            return removed;
        });
    }

    public void ReplaceSubscription(string source, IReadOnlyList<ProxyNode> nodes)
    {
        ExecuteWrite(() =>
        {
            RefreshFromDisk();
            Dictionary<string, ProxyNode> existing = _nodes.Values
                .Where(node => node.Metadata.GetValueOrDefault("subscriptionSource") == source)
                .ToDictionary(node => node.Id, StringComparer.Ordinal);
            foreach (string id in existing.Keys)
                _nodes.Remove(id);
            foreach (ProxyNode node in nodes)
            {
                ProxyNode replacement = existing.TryGetValue(node.Id, out ProxyNode? previous) && !string.IsNullOrWhiteSpace(previous.Name)
                    ? node with { Name = previous.Name }
                    : node;
                _nodes[replacement.Id] = replacement;
            }
            Save();
            return true;
        });
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

    private void RefreshFromDisk()
    {
        _nodes.Clear();
        foreach (ProxyNode node in Load(_path).Select(Unprotect))
            _nodes[node.Id] = node;
    }

    private T ExecuteWrite<T>(Func<T> action)
    {
        string mutexId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_path)))[..24];
        using Mutex mutex = new(false, $"Rovia.NodeRepository.{mutexId}");
        bool acquired;
        try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(10)); }
        catch (AbandonedMutexException) { acquired = true; }
        if (!acquired)
            throw new IOException("Timed out waiting to update the node repository.");
        try
        {
            lock (_sync)
                return action();
        }
        finally
        {
            mutex.ReleaseMutex();
        }
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
