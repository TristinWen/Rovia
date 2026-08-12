namespace Rovia.Core.Models;

/// <summary>Stores credentials without exposing them through string formatting.</summary>
public sealed record ProxyCredentials(string Username, string? Password = null)
{
    public override string ToString() => "[REDACTED]";
}
