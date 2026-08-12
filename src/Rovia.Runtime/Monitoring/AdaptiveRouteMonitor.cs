using Rovia.Core.Engine;
using Rovia.Core.Models;

namespace Rovia.Runtime.Monitoring;

/// <summary>Periodically re-evaluates routes and switches only when the engine policy allows it.</summary>
public sealed class AdaptiveRouteMonitor(AdaptiveRouteEngine engine, RouteHistoryStore history, TimeSpan interval)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using PeriodicTimer timer = new(interval);
        do
        {
            ProxyNode? previous = engine.CurrentNode;
            ProxyNode? selected = await engine.SelectBestRouteAsync(cancellationToken);
            if (selected is not null && selected.Id != previous?.Id)
            {
                await engine.ConnectAsync(selected, cancellationToken);
                string reason = previous is null ? "initial selection" : $"automatic switch from {previous.Id}";
                history.Append(new(DateTimeOffset.UtcNow, previous?.Id, selected.Id, reason));
            }
        }
        while (await timer.WaitForNextTickAsync(cancellationToken));
    }
}
