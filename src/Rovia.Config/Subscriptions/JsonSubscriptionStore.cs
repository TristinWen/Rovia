using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace Rovia.Config.Subscriptions;

/// <summary>Persists subscription definitions atomically in a UTF-8 JSON document.</summary>
public sealed class JsonSubscriptionStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path = Path.GetFullPath(path);

    public IReadOnlyList<SubscriptionDefinition> GetAll() => File.Exists(_path)
        ? JsonSerializer.Deserialize<List<SubscriptionDefinition>>(File.ReadAllText(_path), JsonOptions) ?? []
        : [];

    public void Upsert(SubscriptionDefinition definition)
    {
        ExecuteWrite(() =>
        {
            Dictionary<string, SubscriptionDefinition> definitions = GetAll().ToDictionary(item => item.Id, StringComparer.Ordinal);
            definitions[definition.Id] = definition;
            Save(definitions.Values);
            return true;
        });
    }

    public bool Remove(string id)
    {
        return ExecuteWrite(() =>
        {
            List<SubscriptionDefinition> definitions = [.. GetAll()];
            bool removed = definitions.RemoveAll(item => item.Id == id) > 0;
            if (removed)
                Save(definitions);
            return removed;
        });
    }

    private void Save(IEnumerable<SubscriptionDefinition> definitions)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        string temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(definitions, JsonOptions));
        File.Move(temporaryPath, _path, true);
    }

    private T ExecuteWrite<T>(Func<T> action)
    {
        string mutexId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_path)))[..24];
        using Mutex mutex = new(false, $"Rovia.SubscriptionStore.{mutexId}");
        bool acquired;
        try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(10)); }
        catch (AbandonedMutexException) { acquired = true; }
        if (!acquired)
            throw new IOException("Timed out waiting to update the subscription store.");
        try { return action(); }
        finally { mutex.ReleaseMutex(); }
    }
}
