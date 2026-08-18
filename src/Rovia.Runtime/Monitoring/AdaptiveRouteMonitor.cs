using Rovia.Core.Engine;
using Rovia.Core.Models;

namespace Rovia.Runtime.Monitoring;

/// <summary>Periodically re-evaluates routes and switches only when the engine policy allows it.</summary>
public sealed class AdaptiveRouteMonitor(
    AdaptiveRouteEngine engine,
    RouteHistoryStore history,
    TimeSpan interval,
    HealthHistoryStore? healthHistory = null,
    RouteEvaluationSignal? evaluationSignal = null)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            ProxyNode? previous = engine.CurrentNode;
            ProxyNode? selected = await engine.SelectBestRouteAsync(cancellationToken);
            healthHistory?.Append(engine.GetHealth().Values);
            if (selected is not null && selected.Id != previous?.Id)
            {
                await engine.ConnectAsync(selected, cancellationToken);
                string reason = engine.LastSelectionDecision?.Reason
                    ?? (previous is null ? "Initial selection." : $"Automatic switch from {previous.Id}.");
                history.Append(new(DateTimeOffset.UtcNow, previous?.Id, selected.Id, reason));
            }
            if (evaluationSignal is null)
                await Task.Delay(interval, cancellationToken);
            else
                await evaluationSignal.WaitAsync(interval, cancellationToken);
        }
    }
}
