using Rovia.Platform.Windows.Security;

namespace Rovia.Platform.Windows.Tests;

/// <summary>Validates current-user DPAPI credential protection.</summary>
public sealed class WindowsCredentialProtectorTests
{
    [Fact]
    public void Protect_RoundTripsWithoutPersistingClearText()
    {
        WindowsCredentialProtector protector = new();
        string protectedValue = protector.Protect("private-credential");

        Assert.StartsWith("dpapi:v1:", protectedValue);
        Assert.DoesNotContain("private-credential", protectedValue);
        Assert.Equal("private-credential", protector.Unprotect(protectedValue));
    }
}
