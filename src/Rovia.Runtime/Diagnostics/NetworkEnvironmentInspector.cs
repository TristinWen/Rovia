using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Rovia.Runtime.Diagnostics;

/// <summary>Captures active adapters, DNS, gateways, MTU, and Cloudflare WARP presence.</summary>
public static class NetworkEnvironmentInspector
{
    public static NetworkEnvironmentSnapshot Capture()
    {
        List<NetworkAdapterSnapshot> adapters = [];
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces()
                     .Where(item => item.OperationalStatus == OperationalStatus.Up && item.NetworkInterfaceType != NetworkInterfaceType.Loopback))
        {
            IPInterfaceProperties properties = adapter.GetIPProperties();
            int? mtu = null;
            try { mtu = properties.GetIPv4Properties()?.Mtu; }
            catch (NetworkInformationException) { }
            adapters.Add(new(
                adapter.Name,
                adapter.Description,
                adapter.OperationalStatus.ToString(),
                adapter.NetworkInterfaceType.ToString(),
                IsCloudflareWarp(adapter.Name, adapter.Description),
                mtu,
                properties.UnicastAddresses.Select(item => item.Address.ToString()).ToArray(),
                properties.DnsAddresses.Select(address => address.ToString()).ToArray(),
                properties.GatewayAddresses.Select(item => item.Address.ToString()).Where(value => value is not "0.0.0.0" and not "::").ToArray()));
        }
        return new(DateTimeOffset.UtcNow, adapters);
    }

    public static bool IsCloudflareWarp(string name, string description)
    {
        string identity = $"{name} {description}";
        return identity.Contains("Cloudflare", StringComparison.OrdinalIgnoreCase) ||
               identity.Contains("WARP", StringComparison.OrdinalIgnoreCase);
    }
}
