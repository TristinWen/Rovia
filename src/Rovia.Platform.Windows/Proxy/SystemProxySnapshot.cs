namespace Rovia.Platform.Windows.Proxy;

/// <summary>Captures the current-user Windows Internet proxy settings.</summary>
public sealed record SystemProxySnapshot(bool Enabled, string? Server, string? Override, string? AutoConfigUrl);
