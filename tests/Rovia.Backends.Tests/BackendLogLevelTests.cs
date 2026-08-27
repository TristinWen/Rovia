using Rovia.Backends;

namespace Rovia.Backends.Tests;

/// <summary>Validates severity detection for cores that emit informational logs on stderr.</summary>
public sealed class BackendLogLevelTests
{
    [Theory]
    [InlineData("WARN", "+0800 2026-08-27 INFO inbound connection", "INFO")]
    [InlineData("INFO", "2026/08/27 [Warning] connection closed", "WARN")]
    [InlineData("INFO", "plain message", "INFO")]
    public void Classify_UsesMessageSeverityWhenPresent(string fallback, string message, string expected) =>
        Assert.Equal(expected, BackendLogLevel.Classify(fallback, message));
}
