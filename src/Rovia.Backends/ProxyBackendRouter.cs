using Rovia.Backends.SingBox;
using Rovia.Backends.Xray;
using Rovia.Core.Abstractions;
using Rovia.Core.Models;

namespace Rovia.Backends;

/// <summary>Selects the compatible transport backend without changing existing node links.</summary>
public sealed class ProxyBackendRouter(string dataDirectory, SingBoxOptions singBoxOptions, XrayOptions xrayOptions) : IProxyBackend
{
    private IProxyBackend? _active;

    public BackendCapabilities Capabilities { get; } = new(true, true, true, false,
        new HashSet<ProxyProtocol> { ProxyProtocol.Vless, ProxyProtocol.Vmess, ProxyProtocol.Trojan, ProxyProtocol.Shadowsocks });

    public async Task StartAsync(ProxyNode node, CancellationToken cancellationToken = default)
    {
        _active = await CreateAsync(node, cancellationToken);
        await _active.StartAsync(node, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_active is null)
            return;
        await _active.StopAsync(cancellationToken);
        await _active.DisposeAsync();
        _active = null;
    }

    public async Task SwitchNodeAsync(ProxyNode node, CancellationToken cancellationToken = default)
    {
        await StopAsync(cancellationToken);
        await StartAsync(node, cancellationToken);
    }

    public Task<BackendStatus> GetStatusAsync(CancellationToken cancellationToken = default) => _active?.GetStatusAsync(cancellationToken)
        ?? Task.FromResult(new BackendStatus(false, null, null, null));

    public async ValueTask DisposeAsync() => await StopAsync();

    private async Task<IProxyBackend> CreateAsync(ProxyNode node, CancellationToken cancellationToken)
    {
        if (node.Transport?.Type.Equals("xhttp", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (singBoxOptions.Mode == SingBoxConnectionMode.Tun)
                throw new NotSupportedException("VLESS XHTTP currently supports system-proxy mode only. Select system-proxy before connecting.");
            if (xrayOptions.ListenPort != singBoxOptions.ListenPort)
                throw new InvalidOperationException("Xray and sing-box must expose the same local proxy port.");
            string executable = await new XrayProvisioner().EnsureAsync(dataDirectory, cancellationToken);
            return new XrayBackend(xrayOptions with { ExecutablePath = executable }, new XrayConfigBuilder());
        }
        string singBox = await new SingBoxProvisioner().EnsureAsync(dataDirectory, cancellationToken);
        return new SingBoxBackend(singBoxOptions with { ExecutablePath = singBox }, new SingBoxConfigBuilder());
    }
}
