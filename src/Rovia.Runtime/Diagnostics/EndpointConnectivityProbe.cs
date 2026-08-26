using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Rovia.Runtime.Diagnostics;

/// <summary>Diagnoses endpoint DNS, each resolved TCP path, and normal certificate-validating TLS.</summary>
public sealed class EndpointConnectivityProbe(TimeSpan? timeout = null)
{
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(5);

    public async Task<EndpointConnectivityResult> ProbeAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        IPAddress[] addresses;
        try { addresses = await Dns.GetHostAddressesAsync(host, cancellationToken); }
        catch (SocketException exception)
        {
            return new(host, port, [], false, null, null, null, $"DNS resolution failed: {exception.Message}");
        }

        List<EndpointAddressResult> addressResults = [];
        foreach (IPAddress address in addresses)
            addressResults.Add(await ProbeTcpAsync(address, port, cancellationToken));

        try
        {
            using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(_timeout);
            using TcpClient client = new();
            await client.ConnectAsync(host, port, timeoutSource.Token);
            using SslStream tls = new(client.GetStream(), false);
            await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = host,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            }, timeoutSource.Token);
            X509Certificate2? certificate = tls.RemoteCertificate is null ? null : new(tls.RemoteCertificate);
            return new(host, port, addressResults, true, tls.SslProtocol.ToString(), certificate?.Subject, certificate?.Issuer, null);
        }
        catch (Exception exception) when (exception is IOException or AuthenticationException or SocketException or OperationCanceledException)
        {
            return new(host, port, addressResults, false, null, null, null, $"TLS validation failed: {exception.Message}");
        }
    }

    private async Task<EndpointAddressResult> ProbeTcpAsync(IPAddress address, int port, CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_timeout);
        try
        {
            using TcpClient client = new(address.AddressFamily);
            await client.ConnectAsync(address, port, timeoutSource.Token);
            return new(address.ToString(), true, stopwatch.Elapsed.TotalMilliseconds, null);
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
            return new(address.ToString(), false, null, exception.Message);
        }
    }
}
