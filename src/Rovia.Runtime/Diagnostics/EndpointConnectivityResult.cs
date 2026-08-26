namespace Rovia.Runtime.Diagnostics;

/// <summary>Reports DNS, per-address TCP, and validated TLS evidence for an endpoint.</summary>
public sealed record EndpointConnectivityResult(
    string Host,
    int Port,
    IReadOnlyList<EndpointAddressResult> Addresses,
    bool TlsSucceeded,
    string? TlsProtocol,
    string? CertificateSubject,
    string? CertificateIssuer,
    string? TlsError);
