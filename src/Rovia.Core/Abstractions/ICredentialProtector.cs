namespace Rovia.Core.Abstractions;

/// <summary>Protects persisted credential values for the current operating-system user.</summary>
public interface ICredentialProtector
{
    string Protect(string value);
    string Unprotect(string value);
    bool IsProtected(string value);
}
