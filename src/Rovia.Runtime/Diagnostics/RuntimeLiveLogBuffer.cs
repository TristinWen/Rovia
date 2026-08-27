using System.Text.RegularExpressions;

namespace Rovia.Runtime.Diagnostics;

/// <summary>Keeps a bounded, redacted backend log stream in memory only.</summary>
public sealed class RuntimeLiveLogBuffer(int capacity = 500)
{
    private static readonly Regex UuidPattern = new(@"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex QueryPattern = new(@"(https?://[^\s?]+)\?[^\s]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly object _sync             = new();
    private readonly Queue<RuntimeLiveLogEntry> _entries = new();
    private long _sequence;

    public void Add(string level, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;
        string redacted = QueryPattern.Replace(UuidPattern.Replace(message.Trim(), "[uuid]"), "$1?[redacted]");
        lock (_sync)
        {
            _entries.Enqueue(new(++_sequence, DateTimeOffset.UtcNow, level, redacted));
            while (_entries.Count > capacity)
                _entries.Dequeue();
        }
    }

    public IReadOnlyList<RuntimeLiveLogEntry> Snapshot()
    {
        lock (_sync)
            return _entries.ToArray();
    }

    public IReadOnlyList<RuntimeLiveLogEntry> Since(long sequence)
    {
        lock (_sync)
            return _entries.Where(entry => entry.Sequence > sequence).ToArray();
    }
}

/// <summary>Represents one transient backend log line exposed to the desktop.</summary>
public sealed record RuntimeLiveLogEntry(long Sequence, DateTimeOffset Timestamp, string Level, string Message);
