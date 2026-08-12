namespace Rovia.Core.Models;

/// <summary>Describes TLS and Reality options independent of a backend schema.</summary>
public sealed record TlsOptions(
    bool Enabled,
    string? ServerName = null,
    string? Fingerprint = null,
    string? RealityPublicKey = null,
    string? RealityShortId = null);
