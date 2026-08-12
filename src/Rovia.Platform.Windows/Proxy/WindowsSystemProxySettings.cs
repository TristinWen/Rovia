using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Rovia.Platform.Windows.Proxy;

/// <summary>Controls current-user Windows Internet proxy settings through the registry and WinINet.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsSystemProxySettings : ISystemProxySettings
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

    public SystemProxySnapshot Read()
    {
        EnsureWindows();
        using RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath) ?? throw new InvalidOperationException("Windows Internet settings are unavailable.");
        return new(Convert.ToInt32(key.GetValue("ProxyEnable", 0)) != 0, key.GetValue("ProxyServer") as string,
            key.GetValue("ProxyOverride") as string, key.GetValue("AutoConfigURL") as string);
    }

    public void Apply(SystemProxySnapshot snapshot)
    {
        EnsureWindows();
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath);
        key.SetValue("ProxyEnable", snapshot.Enabled ? 1 : 0, RegistryValueKind.DWord);
        SetOrDelete(key, "ProxyServer", snapshot.Server);
        SetOrDelete(key, "ProxyOverride", snapshot.Override);
        SetOrDelete(key, "AutoConfigURL", snapshot.AutoConfigUrl);
        InternetSetOption(IntPtr.Zero, 39, IntPtr.Zero, 0);
        InternetSetOption(IntPtr.Zero, 37, IntPtr.Zero, 0);
    }

    private static void SetOrDelete(RegistryKey key, string name, string? value)
    {
        if (value is null)
            key.DeleteValue(name, false);
        else
            key.SetValue(name, value, RegistryValueKind.String);
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows system proxy integration is only available on Windows.");
    }

    [DllImport("wininet.dll", SetLastError = true)]
    private static extern bool InternetSetOption(IntPtr internet, int option, IntPtr buffer, int bufferLength);
}
