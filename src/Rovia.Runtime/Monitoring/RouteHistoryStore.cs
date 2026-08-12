using System.Text.Json;

namespace Rovia.Runtime.Monitoring;

/// <summary>Appends redacted route changes to a bounded local history.</summary>
public sealed class RouteHistoryStore(string path, int maximumEntries = 100)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public IReadOnlyList<RouteChangeRecord> Read() => File.Exists(path)
        ? JsonSerializer.Deserialize<List<RouteChangeRecord>>(File.ReadAllText(path), JsonOptions) ?? []
        : [];

    public void Append(RouteChangeRecord record)
    {
        List<RouteChangeRecord> records = [.. Read(), record];
        if (records.Count > maximumEntries)
            records.RemoveRange(0, records.Count - maximumEntries);
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(records, JsonOptions));
    }
}
