using Rovia.Runtime.Runtime;

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

}
