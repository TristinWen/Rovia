namespace Rovia.Platform.Windows.Proxy;

/// <summary>Reads, applies, and refreshes current-user system proxy settings.</summary>
public interface ISystemProxySettings
{
    SystemProxySnapshot Read();
    void Apply(SystemProxySnapshot snapshot);
}
