using Rovia.Core.Abstractions;
using Rovia.Core.Models;

namespace Rovia.Core.Health;

/// <summary>Tracks recent probe outcomes and derives stable health metrics.</summary>
public sealed class HealthMonitor(INodeProbe probe, int historySize = 10)
{
    private readonly Dictionary<string, Queue<NodeHealth>> _history = new(StringComparer.Ordinal);

    public async Task<NodeHealth> CheckAsync(ProxyNode node, CancellationToken cancellationToken = default)
    {
        NodeHealth sample = await probe.ProbeAsync(node, cancellationToken);
        if (!_history.TryGetValue(node.Id, out Queue<NodeHealth>? history))
            _history[node.Id] = history = new Queue<NodeHealth>();
        history.Enqueue(sample);
        while (history.Count > historySize)
            history.Dequeue();

        int failures       = history.Reverse().TakeWhile(item => item.State is NodeHealthState.Offline or NodeHealthState.Unhealthy).Count();
        double successRate = history.Count(item => item.State is NodeHealthState.Healthy or NodeHealthState.Degraded) / (double)history.Count;
        double? jitter     = CalculateJitter(history);
        return sample with { ConsecutiveFailures = failures, SuccessRate = successRate, JitterMs = jitter };
    }

    private static double? CalculateJitter(IEnumerable<NodeHealth> samples)
    {
        double[] values = samples.Where(item => item.LatencyMs.HasValue).Select(item => item.LatencyMs!.Value).ToArray();
        if (values.Length < 2)
            return null;
        double average = values.Average();
        return Math.Sqrt(values.Average(value => Math.Pow(value - average, 2)));
    }
}
