namespace Rovia.Backends.SingBox;

using Rovia.Core.Policies;

/// <summary>Configures the sing-box executable and local mixed proxy endpoint.</summary>
public sealed record SingBoxOptions
{
    public string ExecutablePath { get; init; } = "sing-box";
    public string WorkingDirectory { get; init; } = Path.Combine(Path.GetTempPath(), "rovia");
    public string ListenAddress { get; init; } = "127.0.0.1";
    public int ListenPort { get; init; } = 2080;
    public SingBoxConnectionMode Mode { get; init; } = SingBoxConnectionMode.SystemProxy;
    public string TunStack { get; init; } = "mixed";
    public IReadOnlyList<RoutingRule> RoutingRules { get; init; } = [];
    public DnsPolicy DnsPolicy { get; init; } = new();
    public string LogLevel { get; init; } = "info";
}
