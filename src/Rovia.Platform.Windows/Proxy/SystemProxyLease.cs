using System.Text.Json;

namespace Rovia.Platform.Windows.Proxy;

/// <summary>Activates the Rovia system proxy and restores the exact previous settings when disposed.</summary>
public sealed class SystemProxyLease : IDisposable
{
    private readonly ISystemProxySettings _settings;
    private readonly string _snapshotPath;
    private bool _restored;

    private SystemProxyLease(ISystemProxySettings settings, string snapshotPath)
    {
        _settings     = settings;
        _snapshotPath = snapshotPath;
    }

    public static SystemProxyLease Activate(ISystemProxySettings settings, string snapshotPath, string proxyServer)
    {
        SystemProxySnapshot previous = settings.Read();
        string fullPath              = Path.GetFullPath(snapshotPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(previous));
        settings.Apply(new(true, proxyServer, "<local>", null));
        return new(settings, fullPath);
    }

    public static bool RestorePending(ISystemProxySettings settings, string snapshotPath)
    {
        if (!File.Exists(snapshotPath))
            return false;
        SystemProxySnapshot snapshot = JsonSerializer.Deserialize<SystemProxySnapshot>(File.ReadAllText(snapshotPath))
            ?? throw new IOException("The saved system proxy snapshot is invalid.");
        settings.Apply(snapshot);
        File.Delete(snapshotPath);
        return true;
    }

    public void Dispose()
    {
        if (_restored)
            return;
        RestorePending(_settings, _snapshotPath);
        _restored = true;
    }
}
