using System.Net.Sockets;

namespace Rovia.Backends;

/// <summary>Waits until a backend accepts local proxy connections or exits during startup.</summary>
public sealed class BackendReadinessProbe(TimeSpan? timeout = null, TimeSpan? retryInterval = null)
{
    private readonly TimeSpan _timeout       = timeout ?? TimeSpan.FromSeconds(10);
    private readonly TimeSpan _retryInterval = retryInterval ?? TimeSpan.FromMilliseconds(100);

    public async Task WaitAsync(
        string host,
        int port,
        Func<bool> hasExited,
        Func<string?> errorProvider,
        string backendName,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(_timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (hasExited())
                throw new InvalidOperationException($"{backendName} exited before its local proxy became ready. {errorProvider()}".Trim());
            try
            {
                using TcpClient client = new();
                using CancellationTokenSource attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                attempt.CancelAfter(TimeSpan.FromMilliseconds(500));
                await client.ConnectAsync(host, port, attempt.Token);
                return;
            }
            catch (Exception exception) when ((exception is SocketException or OperationCanceledException) && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_retryInterval, cancellationToken);
            }
        }
        throw new TimeoutException($"{backendName} did not open its local proxy at {host}:{port} within {_timeout.TotalSeconds:0} seconds. {errorProvider()}".Trim());
    }
}
