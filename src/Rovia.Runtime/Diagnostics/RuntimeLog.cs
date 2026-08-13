using System.Text.Json;

namespace Rovia.Runtime.Diagnostics;

/// <summary>Writes bounded structured runtime events without accepting credential fields.</summary>
public sealed class RuntimeLog(string directory, long maximumBytes = 1_048_576, int retainedFiles = 3)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private readonly object _sync = new();

    public void Write(string level, string eventName, string message)
    {
        Directory.CreateDirectory(directory);
        lock (_sync)
        {
            string path = Path.Combine(directory, "rovia.jsonl");
            if (File.Exists(path) && new FileInfo(path).Length >= maximumBytes)
                Rotate(path);
            string entry = JsonSerializer.Serialize(new LogEntry(DateTimeOffset.UtcNow, level, eventName, message), JsonOptions);
            File.AppendAllText(path, entry + Environment.NewLine);
        }
    }

    private void Rotate(string path)
    {
        for (int index = retainedFiles - 1; index >= 1; index--)
        {
            string source      = $"{path}.{index}";
            string destination = $"{path}.{index + 1}";
            if (File.Exists(source))
                File.Move(source, destination, true);
        }
        File.Move(path, $"{path}.1", true);
    }

    private sealed record LogEntry(DateTimeOffset Timestamp, string Level, string Event, string Message);
}
