using System.Threading.Channels;

namespace Rovia.Runtime.Monitoring;

/// <summary>Coalesces external events that require immediate route re-evaluation.</summary>
public sealed class RouteEvaluationSignal
{
    private readonly Channel<bool> _events = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = false
    });

    public void Pulse() => _events.Writer.TryWrite(true);

    public async Task WaitAsync(TimeSpan maximumDelay, CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task delay = Task.Delay(maximumDelay, delayCancellation.Token);
        Task<bool> signal = _events.Reader.WaitToReadAsync(cancellationToken).AsTask();
        Task completed = await Task.WhenAny(delay, signal);
        if (completed == signal && await signal)
        {
            _events.Reader.TryRead(out _);
            delayCancellation.Cancel();
        }
        await completed;
    }
}
