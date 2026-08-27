using System.Diagnostics;
using Rovia.Core.Abstractions;
using Rovia.Core.Models;

namespace Rovia.Backends.Xray;

/// <summary>Runs Xray for VLESS XHTTP while exposing a local HTTP proxy.</summary>
public sealed class XrayBackend(
    XrayOptions options,
    XrayConfigBuilder configBuilder,
    BackendReadinessProbe? readinessProbe = null,
    Action<string, string>? liveLog = null) : IProxyBackend
{
    private readonly BackendReadinessProbe _readinessProbe = readinessProbe ?? new();
    private Process? _process;
    private string?  _nodeId;
    private string?  _lastError;

    public BackendCapabilities Capabilities { get; } = new(false, true, true, false, new HashSet<ProxyProtocol> { ProxyProtocol.Vless });

    public async Task StartAsync(ProxyNode node, CancellationToken cancellationToken = default)
    {
        if (_process is { HasExited: false })
            throw new InvalidOperationException("Xray is already running.");
        Directory.CreateDirectory(options.WorkingDirectory);
        string configPath = Path.Combine(options.WorkingDirectory, "xray.json");
        await File.WriteAllTextAsync(configPath, configBuilder.Build(node, options), cancellationToken);
        ProcessStartInfo startInfo = new()
        {
            FileName               = options.ExecutablePath,
            WorkingDirectory       = options.WorkingDirectory,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(configPath);
        _process = new() { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.ErrorDataReceived += (_, eventArgs) => Capture("WARN", eventArgs.Data);
        _process.OutputDataReceived += (_, eventArgs) => Capture("INFO", eventArgs.Data);
        if (!_process.Start())
            throw new InvalidOperationException("Unable to start Xray.");
        _process.BeginErrorReadLine();
        _process.BeginOutputReadLine();
        try
        {
            await _readinessProbe.WaitAsync(options.ListenAddress, options.ListenPort,
                () => _process.HasExited, () => _lastError, "Xray", cancellationToken);
            _nodeId = node.Id;
        }
        catch
        {
            await StopAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_process is null)
            return;
        if (!_process.HasExited)
        {
            _process.Kill(true);
            await _process.WaitForExitAsync(cancellationToken);
        }
        _process.Dispose();
        _process = null;
        _nodeId  = null;
    }

    public async Task SwitchNodeAsync(ProxyNode node, CancellationToken cancellationToken = default)
    {
        await StopAsync(cancellationToken);
        await StartAsync(node, cancellationToken);
    }

    public Task<BackendStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        bool running         = _process is { HasExited: false };
        BackendStatus status = new(running, running ? _process!.Id : null, _nodeId,
            running ? new Uri($"http://{options.ListenAddress}:{options.ListenPort}") : null, _lastError, "Xray");
        return Task.FromResult(status);
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private void Capture(string level, string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;
        string classified = BackendLogLevel.Classify(level, message);
        _lastError = classified is "WARN" or "ERROR" ? message : _lastError;
        liveLog?.Invoke(classified, message);
    }
}
