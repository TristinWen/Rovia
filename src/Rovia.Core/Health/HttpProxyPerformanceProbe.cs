using System.Diagnostics;
using System.Net;
using Rovia.Core.Abstractions;
using Rovia.Core.Models;

namespace Rovia.Core.Health;

/// <summary>Measures warmed HTTP latency and bounded streaming throughput with connection reuse.</summary>
public sealed class HttpProxyPerformanceProbe : IProxyPerformanceProbe, IDisposable
{
    private readonly Uri _latencyTarget;
    private readonly Uri _downloadTarget;
    private readonly int _latencySamples;
    private readonly long _maximumBytes;
    private readonly TimeSpan _maximumDownloadDuration;
    private readonly TimeSpan _timeout;
    private HttpClient? _client;
    private Uri? _proxyEndpoint;

    public HttpProxyPerformanceProbe(
        Uri? latencyTarget = null,
        Uri? downloadTarget = null,
        int latencySamples = 3,
        long maximumBytes = 512 * 1024,
        TimeSpan? maximumDownloadDuration = null,
        TimeSpan? timeout = null)
    {
        if (latencySamples < 1)
            throw new ArgumentOutOfRangeException(nameof(latencySamples));
        if (maximumBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        _latencyTarget  = latencyTarget ?? new("https://www.google.com/generate_204");
        _downloadTarget = downloadTarget ?? new("http://speedtest.tele2.net/10MB.zip");
        _latencySamples = latencySamples;
        _maximumBytes   = maximumBytes;
        _maximumDownloadDuration = maximumDownloadDuration ?? TimeSpan.FromSeconds(3);
        _timeout        = timeout ?? TimeSpan.FromSeconds(20);
    }

    public async Task<ProxyPerformance> MeasureAsync(Uri proxyEndpoint, CancellationToken cancellationToken = default)
    {
        HttpClient client = GetClient(proxyEndpoint);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        try
        {
            await SendLatencyRequestAsync(client, timeout.Token);
            List<double> samples = new(_latencySamples);
            for (int index = 0; index < _latencySamples; index++)
                samples.Add(await SendLatencyRequestAsync(client, timeout.Token));
            samples.Sort();
            double latency = samples[samples.Count / 2];
            try
            {
                (long bytes, TimeSpan duration) = await MeasureDownloadAsync(client, timeout.Token);
                double mbps = duration.TotalSeconds > 0 ? bytes * 8d / duration.TotalSeconds / 1_000_000d : 0;
                return new(latency, mbps, bytes, DateTimeOffset.UtcNow, "Performance measurement completed.");
            }
            catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or IOException)
            {
                string message = exception is OperationCanceledException ? "Download measurement timed out; latency is available." : $"Download measurement failed; latency is available. {exception.Message}";
                return new(latency, null, 0, DateTimeOffset.UtcNow, message);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or IOException)
        {
            return new(null, null, 0, DateTimeOffset.UtcNow, exception is OperationCanceledException ? "Performance measurement timed out." : exception.Message);
        }
    }

    private HttpClient GetClient(Uri proxyEndpoint)
    {
        if (_client is not null && _proxyEndpoint == proxyEndpoint)
            return _client;
        _client?.Dispose();
        _proxyEndpoint = proxyEndpoint;
        _client = new(new SocketsHttpHandler
        {
            Proxy                  = new WebProxy(proxyEndpoint),
            UseProxy               = true,
            ConnectTimeout         = TimeSpan.FromSeconds(5),
            PooledConnectionLifetime = TimeSpan.FromMinutes(10)
        });
        return _client;
    }

    private async Task<double> SendLatencyRequestAsync(HttpClient client, CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        using HttpRequestMessage request = new(HttpMethod.Get, _latencyTarget);
        using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private async Task<(long Bytes, TimeSpan Duration)> MeasureDownloadAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, _downloadTarget);
        using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        byte[] buffer = new byte[64 * 1024];
        long total = 0;
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (total < _maximumBytes && stopwatch.Elapsed < _maximumDownloadDuration)
        {
            int requested = (int)Math.Min(buffer.Length, _maximumBytes - total);
            int read      = await stream.ReadAsync(buffer.AsMemory(0, requested), cancellationToken);
            if (read == 0)
                break;
            total += read;
        }
        stopwatch.Stop();
        return (total, stopwatch.Elapsed);
    }

    public void Dispose() => _client?.Dispose();
}
