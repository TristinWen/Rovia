using Rovia.Runtime.Diagnostics;

namespace Rovia.Runtime.Tests;

/// <summary>Validates bounded transient backend logs and secret redaction.</summary>
public sealed class RuntimeLiveLogBufferTests
{
    [Fact]
    public void Snapshot_RedactsSecretsAndKeepsNewestEntries()
    {
        RuntimeLiveLogBuffer buffer = new(2);
        buffer.Add("INFO", "old");
        buffer.Add("INFO", "accepted https://example.com/path?token=secret");
        buffer.Add("WARN", "user 11111111-1111-1111-1111-111111111111 disconnected");

        IReadOnlyList<RuntimeLiveLogEntry> entries = buffer.Snapshot();

        Assert.Equal(2, entries.Count);
        Assert.DoesNotContain("secret", entries[0].Message);
        Assert.Contains("?[redacted]", entries[0].Message);
        Assert.DoesNotContain("11111111", entries[1].Message);
        Assert.Equal([2L, 3L], entries.Select(entry => entry.Sequence));
    }
}
