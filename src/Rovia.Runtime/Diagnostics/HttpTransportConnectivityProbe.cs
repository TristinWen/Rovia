using System.Diagnostics;
using System.Net.Http;

namespace Rovia.Runtime.Diagnostics;

/// <summary>Validates an HTTP transport path without WebSocket upgrade headers.</summary>
public sealed class HttpTransportConnectivityProbe(TimeSpan? timeout = null)
{
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(8);

    public async Task<HttpConnectivityResult> ProbeAsync(
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
        using HttpClient client = new(new SocketsHttpHandler { AllowAutoRedirect = false });
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, target);
            if (!string.IsNullOrWhiteSpace(hostHeader) && !string.Equals(hostHeader, host, StringComparison.OrdinalIgnoreCase))
                request.Headers.Host = hostHeader;
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token);
            stopwatch.Stop();
            return new(target, true, stopwatch.Elapsed.TotalMilliseconds, (int)response.StatusCode, null);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or IOException)
        {
            return new(target, false, stopwatch.Elapsed.TotalMilliseconds, null, exception.Message);
        }
    }

    public static Uri BuildUri(string host, int port, bool tls, string? path)
    {
        string scheme       = tls ? "https" : "http";
        string normalizedPath = string.IsNullOrWhiteSpace(path) ? "/" : path.StartsWith('/') ? path : $"/{path}";
        return new UriBuilder(scheme, host, port, normalizedPath).Uri;
    }
}