using System.Net;
using System.Net.Sockets;
using Rovia.Backends;

namespace Rovia.Backends.Tests;

/// <summary>Validates backend startup readiness and early-exit reporting.</summary>
public sealed class BackendReadinessProbeTests
{
    [Fact]
    public async Task WaitAsync_WaitsForDelayedListener()
    {
        int port;
        using (TcpListener reservation = new(IPAddress.Loopback, 0))
        {
            reservation.Start();
            port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        }
        using TcpListener listener = new(IPAddress.Loopback, port);
        Task start = Task.Run(async () =>
        {
            await Task.Delay(150);
            listener.Start();
        });

        await new BackendReadinessProbe(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(20))
            .WaitAsync("127.0.0.1", port, () => false, () => null, "test");

        await start;
    }

    [Fact]
    public async Task WaitAsync_ReportsBackendExitBeforeTimeout()
    {
        BackendReadinessProbe probe = new(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(20));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            probe.WaitAsync("127.0.0.1", 1, () => true, () => "bad config", "Xray"));

        Assert.Contains("bad config", exception.Message);
    }
}
