namespace Rovia.Core.Models;

/// <summary>Classifies failures observed through a running proxy route.</summary>
public enum EgressFailureKind
{
    None,
    Timeout,
    NameResolution,
    Tls,
    Authentication,
    Http,
    Connection
}
