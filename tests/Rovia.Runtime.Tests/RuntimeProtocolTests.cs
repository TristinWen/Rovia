using Rovia.Runtime.Runtime;

namespace Rovia.Runtime.Tests;

/// <summary>Locks the Desktop-to-runtime compatibility contract to an explicit positive version.</summary>
public sealed class RuntimeProtocolTests
{
    [Fact]
    public void Version_IsPositive() => Assert.True(RuntimeProtocol.Version > 0);
}
