using System.Net;
using System.Net.Sockets;
using Rovia.Runtime.Runtime;

namespace Rovia.Runtime.Tests;

/// <summary>Validates proxy runtime preflight checks.</summary>
public sealed class RuntimePreflightTests
{
    [Fact]
    public void EnsurePortAvailable_RejectsOccupiedPort()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        try
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => RuntimePreflight.EnsurePortAvailable(port));
            Assert.Contains("already in use", exception.Message);
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public void EnsurePortAvailable_AcceptsFreePort()
    {
        TcpListener reservation = new(IPAddress.Loopback, 0);
        reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();

        RuntimePreflight.EnsurePortAvailable(port);
    }
}
