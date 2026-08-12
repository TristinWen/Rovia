using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using Rovia.Core.Abstractions;
using Rovia.Core.Models;

namespace Rovia.Core.Health;

/// <summary>Verifies HTTP traffic through a proxy and reports actionable failure categories.</summary>
public sealed class HttpEgressProbe : IEgressProbe
{
    private readonly TimeSpan _timeout;
    private readonly Func<Uri, HttpMessageHandler> _handlerFactory;

    public HttpEgressProbe(TimeSpan? timeout = null) : this(timeout ?? TimeSpan.FromSeconds(10), CreateHandler) { }

    public HttpEgressProbe(TimeSpan timeout, Func<Uri, HttpMessageHandler> handlerFactory)
    {
        _timeout        = timeout;
        _handlerFactory = handlerFactory;
    }

    public async Task<EgressProbeResult> ProbeAsync(Uri proxyEndpoint, Uri target, CancellationToken cancellationToken = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_timeout);
        try
        {
            using HttpClient client = new(_handlerFactory(proxyEndpoint), true);
            using HttpRequestMessage request = new(HttpMethod.Get, target);
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token);
            stopwatch.Stop();
            bool success = response.IsSuccessStatusCode;
            return new(success, target, stopwatch.Elapsed, (int)response.StatusCode,
                success ? EgressFailureKind.None : ClassifyStatus(response.StatusCode),
                success ? "Proxy egress is reachable." : $"The target returned HTTP {(int)response.StatusCode}.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(target, stopwatch.Elapsed, EgressFailureKind.Timeout, "The proxy request timed out.");
        }
        catch (HttpRequestException exception)
        {
            EgressFailureKind kind = exception.InnerException switch
            {
                System.Net.Sockets.SocketException socket when socket.SocketErrorCode == System.Net.Sockets.SocketError.HostNotFound => EgressFailureKind.NameResolution,
                AuthenticationException => EgressFailureKind.Tls,
                _ => EgressFailureKind.Connection
            };
            return Failure(target, stopwatch.Elapsed, kind, exception.Message);
        }
    }

    private static HttpMessageHandler CreateHandler(Uri proxyEndpoint) => new SocketsHttpHandler
    {
        Proxy             = new WebProxy(proxyEndpoint),
        UseProxy          = true,
        ConnectTimeout    = TimeSpan.FromSeconds(5),
        AllowAutoRedirect = false
    };

    private static EgressFailureKind ClassifyStatus(HttpStatusCode code) => code is HttpStatusCode.Unauthorized or HttpStatusCode.ProxyAuthenticationRequired
        ? EgressFailureKind.Authentication
        : EgressFailureKind.Http;

    private static EgressProbeResult Failure(Uri target, TimeSpan duration, EgressFailureKind kind, string message) =>
        new(false, target, duration, null, kind, message);
}
