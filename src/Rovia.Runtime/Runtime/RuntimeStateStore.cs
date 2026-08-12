using System.Text.Json;

namespace Rovia.Runtime.Runtime;

/// <summary>Persists runtime state atomically for cross-process inspection.</summary>
public sealed class RuntimeStateStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public RuntimeState? Read()
    {
        if (!File.Exists(path))
            return null;
        RuntimeState? state = JsonSerializer.Deserialize<RuntimeState>(File.ReadAllText(path), JsonOptions);
        if (state is not null && !IsProcessAlive(state.ProcessId))
            return state with { IsRunning = false, UpdatedAt = DateTimeOffset.UtcNow, LastMessage = "Runtime process is no longer running." };
        return state;
    }

    public void Write(RuntimeState state)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporaryPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state, JsonOptions));
        File.Move(temporaryPath, fullPath, true);
    }

    private static bool IsProcessAlive(int processId)
    {
        try { return !System.Diagnostics.Process.GetProcessById(processId).HasExited; }
        catch (ArgumentException) { return false; }
    }
}
