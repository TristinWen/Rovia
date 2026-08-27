using Rovia.Runtime.Runtime;
using Rovia.Runtime.Diagnostics;

namespace Rovia.Runtime.Tests;

/// <summary>Validates cross-process runtime state persistence.</summary>
public sealed class RuntimeStateStoreTests
{
    [Fact]
    public void Read_MarksMissingProcessAsStopped()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"rovia-runtime-{Guid.NewGuid():N}");
        string path      = Path.Combine(directory, "state.json");
        try
        {
            RuntimeStateStore store = new(path);
            store.Write(new RuntimeState { IsRunning = true, ProcessId = int.MaxValue, StartedAt = DateTimeOffset.UtcNow });

            RuntimeState? state = store.Read();

            Assert.False(state?.IsRunning);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Read_PreservesCleanStopMessage()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"rovia-runtime-{Guid.NewGuid():N}");
        string path      = Path.Combine(directory, "state.json");
        try
        {
            RuntimeStateStore store = new(path);
            store.Write(new RuntimeState { IsRunning = false, ProcessId = int.MaxValue, LastMessage = "Disconnected cleanly." });

            RuntimeState? state = store.Read();

            Assert.Equal("Disconnected cleanly.", state?.LastMessage);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Write_DoesNotPersistTransientBackendLogs()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"rovia-runtime-{Guid.NewGuid():N}");
        string path      = Path.Combine(directory, "state.json");
        try
        {
            RuntimeStateStore store = new(path);
            store.Write(new RuntimeState
            {
                LiveLogs = [new RuntimeLiveLogEntry(1, DateTimeOffset.UtcNow, "INFO", "accepted example.com:443")]
            });

            Assert.Empty(store.Read()!.LiveLogs);
            Assert.DoesNotContain("example.com", File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
