using System.Diagnostics;
using System.Net.WebSockets;

namespace Rovia.Runtime.Diagnostics;

/// <summary>Validates a standard WebSocket upgrade without sending proxy payload data.</summary>
public sealed class WebSocketConnectivityProbe(TimeSpan? timeout = null)
{
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(8);

    public async Task<WebSocketConnectivityResult> ProbeAsync(
        string host,
        int port,
        bool tls,
        string? path,
        string? hostHeader,
        CancellationToken cancellationToken = default)
    {
        Uri target = BuildUri(host, port, tls, path);
        Stopwatch stopwatch = Stopwatch.StartNew();
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_timeout);
        using ClientWebSocket socket = new();
        try
        {
            if (!string.IsNullOrWhiteSpace(hostHeader) && !string.Equals(hostHeader, host, StringComparison.OrdinalIgnoreCase))
                socket.Options.SetRequestHeader("Host", hostHeader);
            await socket.ConnectAsync(target, timeoutSource.Token);
            socket.Abort();
            return new(target, true, stopwatch.Elapsed.TotalMilliseconds, null);
        }
        catch (Exception exception) when (exception is WebSocketException or HttpRequestException or OperationCanceledException or ArgumentException)
        {
            return new(target, false, stopwatch.Elapsed.TotalMilliseconds, exception.Message);
        }
    }

    public static Uri BuildUri(string host, int port, bool tls, string? path)
    {
        string normalizedPath = string.IsNullOrWhiteSpace(path) ? "/" : path.StartsWith('/') ? path : $"/{path}";
        return new UriBuilder(tls ? "wss" : "ws", host, port, normalizedPath).Uri;
    }
}
