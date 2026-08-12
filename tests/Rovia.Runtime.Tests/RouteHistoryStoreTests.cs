using Rovia.Runtime.Monitoring;

namespace Rovia.Runtime.Tests;

/// <summary>Validates bounded route change history.</summary>
public sealed class RouteHistoryStoreTests
{
    [Fact]
    public void Append_KeepsNewestRecords()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"rovia-history-{Guid.NewGuid():N}");
        string path      = Path.Combine(directory, "history.json");
        try
        {
            RouteHistoryStore store = new(path, 2);
            store.Append(new(DateTimeOffset.UtcNow, null, "a", "first"));
            store.Append(new(DateTimeOffset.UtcNow, "a", "b", "second"));
            store.Append(new(DateTimeOffset.UtcNow, "b", "c", "third"));

            IReadOnlyList<RouteChangeRecord> records = store.Read();

            Assert.Equal(["b", "c"], records.Select(record => record.CurrentNodeId));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
