using System.Text.Json;
using Rovia.Core.Models;

namespace Rovia.Runtime.Monitoring;

/// <summary>Persists bounded redacted node-health evidence for route explanations.</summary>
public sealed class HealthHistoryStore(string path, int maximumEntries = 1000)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path = Path.GetFullPath(path);

    public IReadOnlyList<NodeHealth> Read() => File.Exists(_path)
        ? JsonSerializer.Deserialize<List<NodeHealth>>(File.ReadAllText(_path), JsonOptions) ?? []
        : [];

    public void Append(IEnumerable<NodeHealth> samples)
    {
        List<NodeHealth> history = [.. Read(), .. samples];
        if (history.Count > maximumEntries)
            history.RemoveRange(0, history.Count - maximumEntries);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        string temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(history, JsonOptions));
        File.Move(temporaryPath, _path, true);
    }
}
