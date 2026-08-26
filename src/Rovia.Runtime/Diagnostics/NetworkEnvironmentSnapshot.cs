namespace Rovia.Runtime.Diagnostics;

/// <summary>Summarizes active network paths relevant to proxy connectivity.</summary>
public sealed record NetworkEnvironmentSnapshot(DateTimeOffset CapturedAt, IReadOnlyList<NetworkAdapterSnapshot> Adapters)
{
    public bool CloudflareWarpDetected => Adapters.Any(adapter => adapter.IsCloudflareWarp);
}
