using System.Diagnostics;
using System.Net.Sockets;
using Rovia.Core.Abstractions;
using Rovia.Core.Models;

namespace Rovia.Core.Health;

/// <summary>Measures node reachability using a bounded TCP handshake.</summary>
public sealed class TcpNodeProbe(TimeSpan timeout) : INodeProbe
{
    public TcpNodeProbe() : this(TimeSpan.FromSeconds(5)) { }

    public async Task<NodeHealth> ProbeAsync(ProxyNode node, CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            using TcpClient client = new();
            await client.ConnectAsync(node.Host, node.Port, timeoutSource.Token);
            stopwatch.Stop();
            return new NodeHealth
            {
                NodeId = node.Id, LatencyMs = stopwatch.Elapsed.TotalMilliseconds,
                TcpHandshakeMs = stopwatch.Elapsed.TotalMilliseconds, SuccessRate = 1,
                State = NodeHealthState.Healthy, LastCheckedAt = DateTimeOffset.UtcNow
            };
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
            return new NodeHealth
            {
                NodeId = node.Id, SuccessRate = 0, ConsecutiveFailures = 1,
                State = NodeHealthState.Offline, LastCheckedAt = DateTimeOffset.UtcNow
            };
        }
    }
}
