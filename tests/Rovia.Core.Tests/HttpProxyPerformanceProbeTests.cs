using System.Net;
using System.Net.Sockets;
using System.Text;
using Rovia.Core.Health;
using Rovia.Core.Models;

namespace Rovia.Core.Tests;

/// <summary>Validates warmed latency and bounded throughput measurement through an HTTP proxy.</summary>
public sealed class HttpProxyPerformanceProbeTests
{
    [Fact]
    public async Task MeasureAsync_ReportsLatencyAndBoundedDownload()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using CancellationTokenSource cancellation = new();
        Task server = ServeAsync(listener, cancellation.Token);
        using HttpProxyPerformanceProbe probe = new(
            new Uri($"http://127.0.0.1:{port}/latency"), new Uri($"http://127.0.0.1:{port}/download"),
            latencySamples: 2, maximumBytes: 4096, maximumDownloadDuration: TimeSpan.FromSeconds(1), timeout: TimeSpan.FromSeconds(5));

        ProxyPerformance result = await probe.MeasureAsync(new Uri($"http://127.0.0.1:{port}"));
        cancellation.Cancel();
        listener.Stop();
        try { await server; } catch (Exception exception) when (exception is OperationCanceledException or SocketException) { }

        Assert.NotNull(result.LatencyMs);
        Assert.True(result.DownloadMbps > 0);
        Assert.Equal(4096, result.DownloadedBytes);
    }

    private static async Task ServeAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
            await using NetworkStream stream = client.GetStream();
            using StreamReader reader = new(stream, Encoding.ASCII, leaveOpen: true);
            string requestLine = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;
            string? line;
            do { line = await reader.ReadLineAsync(cancellationToken); } while (!string.IsNullOrEmpty(line));
            bool download = requestLine.Contains("/download", StringComparison.Ordinal);
            byte[] body   = download ? new byte[8192] : [];
            byte[] header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(header, cancellationToken);
            await stream.WriteAsync(body, cancellationToken);
        }
    }
}
