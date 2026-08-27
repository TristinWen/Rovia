namespace Rovia.Backends.Xray;

/// <summary>Configures the Xray executable and local HTTP proxy endpoint.</summary>
public sealed record XrayOptions
{
    public string ExecutablePath   { get; init; } = "xray";
    public string WorkingDirectory { get; init; } = Path.Combine(Path.GetTempPath(), "rovia-xray");
    public string ListenAddress    { get; init; } = "127.0.0.1";
    public int    ListenPort       { get; init; } = 2080;
    public string LogLevel         { get; init; } = "warning";
}
