using Rovia.Core.Models;
using Rovia.Runtime.Monitoring;

namespace Rovia.Runtime.Tests;

public sealed class HealthHistoryStoreTests
{
    [Fact]
    public void Append_KeepsNewestHealthEvidence()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"rovia-health-{Guid.NewGuid():N}");
        try
        {
            HealthHistoryStore store = new(Path.Combine(directory, "health.json"), 2);
            store.Append([Health("a", 1), Health("b", 2)]);
            store.Append([Health("c", 3)]);

            Assert.Equal(["b", "c"], store.Read().Select(item => item.NodeId));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private static NodeHealth Health(string id, double latency) => new()
    {
        NodeId = id, LatencyMs = latency, State = NodeHealthState.Healthy, SuccessRate = 1,
        LastCheckedAt = DateTimeOffset.UtcNow
    };
}
