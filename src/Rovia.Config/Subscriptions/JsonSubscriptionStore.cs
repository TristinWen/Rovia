using System.Text.Json;

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
        Dictionary<string, SubscriptionDefinition> definitions = GetAll().ToDictionary(item => item.Id, StringComparer.Ordinal);
        definitions[definition.Id] = definition;
        Save(definitions.Values);
    }

    public bool Remove(string id)
    {
        List<SubscriptionDefinition> definitions = [.. GetAll()];
        bool removed = definitions.RemoveAll(item => item.Id == id) > 0;
        if (removed)
            Save(definitions);
        return removed;
    }

    private void Save(IEnumerable<SubscriptionDefinition> definitions)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        string temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(definitions, JsonOptions));
        File.Move(temporaryPath, _path, true);
    }
}
