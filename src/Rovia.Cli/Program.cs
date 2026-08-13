using System.ComponentModel;
using Rovia.Backends.SingBox;
using Rovia.Config.Parsing;
using Rovia.Config.Storage;
using Rovia.Core.Engine;
using Rovia.Core.Failover;
using Rovia.Core.Health;
using Rovia.Core.Models;
using Rovia.Core.Policies;
using Rovia.Core.Routing;
using Rovia.Platform.Windows.Proxy;
using Rovia.Platform.Windows.Security;
using Rovia.Runtime.Monitoring;
using Rovia.Runtime.Runtime;

return await RoviaCli.RunAsync(args);

/// <summary>Provides the command-line validation host for Rovia Core.</summary>
internal static class RoviaCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        string dataDirectory = Environment.GetEnvironmentVariable("ROVIA_DATA_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rovia");
        JsonNodeRepository repository = new(Path.Combine(dataDirectory, "nodes.json"), new WindowsCredentialProtector());
        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "import"       => Import(args, repository),
                "list"         => List(repository),
                "remove"       => Remove(args, repository),
                "probe"        => await ProbeAsync(repository),
                "rank"         => await RankAsync(repository),
                "connect"      => await ConnectAsync(args, repository, dataDirectory, false),
                "connect-auto" => await ConnectAsync(args, repository, dataDirectory, true),
                "status"       => await StatusAsync(dataDirectory),
                "disconnect"   => await DisconnectAsync(dataDirectory),
                "speed-test"   => await SpeedTestAsync(dataDirectory),
                "diagnose"     => await DiagnoseAsync(dataDirectory),
                "check-config" => CheckConfig(args, repository, dataDirectory),
                _              => Unknown(args[0])
            };
        }
        catch (Exception exception) when (exception is ProxyLinkParseException or InvalidOperationException or IOException or Win32Exception)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            return 2;
        }
    }

    private static int Import(string[] args, JsonNodeRepository repository)
    {
        RequireArguments(args, 2, "import requires a VLESS share link.");
        ProxyNode node = new VlessLinkParser().Parse(args[1]);
        repository.Add(node);
        Console.WriteLine($"imported {node.Id} {DisplayName(node)}");
        return 0;
    }

    private static int List(JsonNodeRepository repository)
    {
        foreach (ProxyNode node in repository.GetAll())
            Console.WriteLine($"{node.Id}\t{node.Protocol.ToString().ToLowerInvariant()}\t{DisplayName(node)}\t{node.Host}:{node.Port}");
        return 0;
    }

    private static int Remove(string[] args, JsonNodeRepository repository)
    {
        RequireArguments(args, 2, "remove requires a node identifier.");
        if (!repository.Remove(args[1]))
            throw new InvalidOperationException($"Node '{args[1]}' was not found.");
        Console.WriteLine($"removed {args[1]}");
        return 0;
    }

    private static async Task<int> ProbeAsync(JsonNodeRepository repository)
    {
        await using AdaptiveRouteEngine engine = CreateEngine(repository, Path.GetTempPath());
        foreach (ProxyNode node in repository.GetAll())
        {
            NodeHealth health = await engine.ProbeAsync(node);
            Console.WriteLine($"{node.Id}\t{health.State.ToString().ToLowerInvariant()}\t{FormatMs(health.TcpHandshakeMs)}");
        }
        return 0;
    }

    private static async Task<int> RankAsync(JsonNodeRepository repository)
    {
        await using AdaptiveRouteEngine engine = CreateEngine(repository, Path.GetTempPath());
        foreach (RouteScore score in await engine.RankAsync())
        {
            ProxyNode node = repository.Get(score.NodeId)!;
            Console.WriteLine($"{score.Value,6:0.0}\t{score.NodeId}\t{DisplayName(node)}\t{score.Reason}");
        }
        return 0;
    }

    private static async Task<int> ConnectAsync(string[] args, JsonNodeRepository repository, string dataDirectory, bool automatic)
    {
        if (!automatic)
            RequireArguments(args, 2, "connect requires a node identifier.");
        using Mutex runtimeMutex = new(false, $"Rovia.Runtime.{Environment.UserName}", out bool ownsRuntime);
        if (!ownsRuntime)
            throw new InvalidOperationException("Another Rovia runtime is already connected or connecting.");
        RecoverInterruptedRuntime(dataDirectory);
        SingBoxOptions requestedOptions = CreateOptions(dataDirectory);
        RuntimePreflight.EnsurePortAvailable(requestedOptions.ListenPort);
        string singBoxPath = await new SingBoxProvisioner().EnsureAsync(dataDirectory);
        await using AdaptiveRouteEngine engine = CreateEngine(repository, dataDirectory, singBoxPath);
        ProxyNode node = automatic
            ? await engine.ConnectBestAsync()
            : repository.Get(args[1]) ?? throw new InvalidOperationException($"Node '{args[1]}' was not found.");
        if (!automatic)
            await engine.ConnectAsync(node);
        BackendStatus status = await engine.GetStatusAsync();
        EgressProbeResult egress = await new HttpEgressProbe().ProbeAsync(status.LocalEndpoint!, new("https://www.google.com/generate_204"));
        if (!egress.Success)
            throw new InvalidOperationException($"Proxy egress check failed ({egress.FailureKind}): {egress.Message}");

        string statePath    = Path.Combine(dataDirectory, "runtime-state.json");
        string historyPath  = Path.Combine(dataDirectory, "route-history.json");
        string snapshotPath = Path.Combine(dataDirectory, "system-proxy.json");
        string pipeName     = $"rovia-{Environment.UserName}";
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        ProxyPerformance? performance = null;
        using HttpProxyPerformanceProbe performanceProbe = new();
        RuntimeStateStore stateStore = new(statePath);
        using CancellationTokenSource exit = new();
        Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; exit.Cancel(); };
        RuntimeState State() => new()
        {
            IsRunning = !exit.IsCancellationRequested, ProcessId = Environment.ProcessId, BackendProcessId = status.ProcessId, NodeId = engine.CurrentNode?.Id,
            NodeName = engine.CurrentNode is null ? null : DisplayName(engine.CurrentNode), LocalEndpoint = status.LocalEndpoint?.ToString(),
            FailoverState = engine.FailoverState, StartedAt = startedAt, UpdatedAt = DateTimeOffset.UtcNow,
            LastMessage = performance?.Message ?? (egress.Success ? $"Egress verified in {egress.Duration.TotalMilliseconds:0} ms." : egress.Message),
            ProxyLatencyMs = performance?.LatencyMs, DownloadMbps = performance?.DownloadMbps, PerformanceAt = performance?.MeasuredAt
        };
        stateStore.Write(State());
        async Task MeasurePerformance(CancellationToken token)
        {
            performance = await performanceProbe.MeasureAsync(status.LocalEndpoint!, token);
            stateStore.Write(State());
        }
        RuntimeControlServer server = new(pipeName, State, exit.Cancel, MeasurePerformance);
        Task serverTask             = server.RunAsync(exit.Token);
        AdaptiveRouteMonitor monitor = new(engine, new RouteHistoryStore(historyPath), TimeSpan.FromSeconds(30));
        Task monitorTask             = automatic ? monitor.RunAsync(exit.Token) : Task.CompletedTask;
        SingBoxOptions activeOptions = CreateOptions(dataDirectory, singBoxPath);
        IDisposable? proxyLease      = OperatingSystem.IsWindows() && activeOptions.Mode == SingBoxConnectionMode.SystemProxy
            ? SystemProxyLease.Activate(new WindowsSystemProxySettings(), snapshotPath, $"127.0.0.1:{activeOptions.ListenPort}")
            : null;
        Console.WriteLine($"connected {DisplayName(node)} via {status.LocalEndpoint}; egress verified");
        Console.WriteLine("Use 'rovia disconnect' or press Ctrl+C to disconnect.");
        try { await Task.Delay(Timeout.InfiniteTimeSpan, exit.Token); } catch (OperationCanceledException) { }
        proxyLease?.Dispose();
        await engine.DisconnectAsync();
        stateStore.Write(State() with { IsRunning = false, LastMessage = "Disconnected cleanly." });
        try { await Task.WhenAll(serverTask, monitorTask); } catch (OperationCanceledException) { }
        return 0;
    }

    private static async Task<int> StatusAsync(string dataDirectory)
    {
        RuntimeState? stored = new RuntimeStateStore(Path.Combine(dataDirectory, "runtime-state.json")).Read();
        if (stored is null)
        {
            Console.WriteLine("stopped");
            return 0;
        }
        RuntimeState state = stored.IsRunning
            ? await new RuntimeControlClient($"rovia-{Environment.UserName}").SendAsync("status")
            : stored;
        Console.WriteLine($"{(state.IsRunning ? "running" : "stopped")}\t{state.NodeName ?? "-"}\t{state.LocalEndpoint ?? "-"}\t{state.LastMessage}");
        if (state.ProxyLatencyMs.HasValue || state.DownloadMbps.HasValue)
            Console.WriteLine($"latency={FormatMetric(state.ProxyLatencyMs, "ms")}\tdownload={FormatMetric(state.DownloadMbps, "Mbps")}");
        return 0;
    }

    private static async Task<int> SpeedTestAsync(string dataDirectory)
    {
        RuntimeState? state = new RuntimeStateStore(Path.Combine(dataDirectory, "runtime-state.json")).Read();
        if (state is not { IsRunning: true })
            throw new InvalidOperationException("Rovia is not connected.");
        RuntimeState measured = await new RuntimeControlClient($"rovia-{Environment.UserName}").SendAsync("speed-test", TimeSpan.FromSeconds(30));
        Console.WriteLine($"latency={FormatMetric(measured.ProxyLatencyMs, "ms")}\tdownload={FormatMetric(measured.DownloadMbps, "Mbps")}");
        return 0;
    }

    private static async Task<int> DiagnoseAsync(string dataDirectory)
    {
        RuntimeState? state = new RuntimeStateStore(Path.Combine(dataDirectory, "runtime-state.json")).Read();
        if (state is not { IsRunning: true } || !Uri.TryCreate(state.LocalEndpoint, UriKind.Absolute, out Uri? endpoint))
            throw new InvalidOperationException("Rovia is not connected.");
        IReadOnlyList<DiagnosticTargetResult> results = await new MultiTargetDiagnostic(new HttpEgressProbe()).RunAsync(endpoint);
        foreach (DiagnosticTargetResult result in results)
            Console.WriteLine($"{result.Target.Host}\tdns={(result.DnsResolved ? "ok" : "failed")}\tegress={(result.Egress.Success ? "ok" : result.Egress.FailureKind.ToString().ToLowerInvariant())}\t{result.Egress.Duration.TotalMilliseconds:0} ms");
        return results.Any(result => result.Egress.Success) ? 0 : 2;
    }

    private static async Task<int> DisconnectAsync(string dataDirectory)
    {
        RuntimeState? state = new RuntimeStateStore(Path.Combine(dataDirectory, "runtime-state.json")).Read();
        if (state is not { IsRunning: true })
        {
            Console.WriteLine("already stopped");
            return 0;
        }
        await new RuntimeControlClient($"rovia-{Environment.UserName}").SendAsync("disconnect");
        Console.WriteLine("disconnect requested");
        return 0;
    }

    private static int CheckConfig(string[] args, JsonNodeRepository repository, string dataDirectory)
    {
        RequireArguments(args, 2, "check-config requires a node identifier.");
        ProxyNode node = repository.Get(args[1]) ?? throw new InvalidOperationException($"Node '{args[1]}' was not found.");
        string json = new SingBoxConfigBuilder().Build(node, CreateOptions(dataDirectory));
        Console.WriteLine(json);
        return 0;
    }

    private static void RecoverInterruptedRuntime(string dataDirectory)
    {
        string statePath    = Path.Combine(dataDirectory, "runtime-state.json");
        string snapshotPath = Path.Combine(dataDirectory, "system-proxy.json");
        RuntimeStateStore store = new(statePath);
        RuntimeState? stale     = store.Read();
        if (stale is { IsRunning: false })
        {
            bool backendStopped = RuntimePreflight.StopOrphanedBackend(stale);
            bool proxyRestored  = OperatingSystem.IsWindows() && SystemProxyLease.RestorePending(new WindowsSystemProxySettings(), snapshotPath);
            if (backendStopped || proxyRestored)
                store.Write(stale with { UpdatedAt = DateTimeOffset.UtcNow, LastMessage = "Recovered resources left by an interrupted runtime." });
        }
    }

    private static AdaptiveRouteEngine CreateEngine(JsonNodeRepository repository, string dataDirectory, string? singBoxPath = null)
    {
        SingBoxOptions options = CreateOptions(dataDirectory, singBoxPath);
        return new(repository, new HealthMonitor(new TcpNodeProbe()), new RouteScorer(), new RouteSelector(),
            new FailoverEngine(), new SingBoxBackend(options, new SingBoxConfigBuilder()), new RoutingPolicy());
    }

    private static SingBoxOptions CreateOptions(string dataDirectory, string? singBoxPath = null) => new()
    {
        ExecutablePath  = singBoxPath ?? Environment.GetEnvironmentVariable("ROVIA_SING_BOX") ?? "sing-box",
        WorkingDirectory = Path.Combine(dataDirectory, "runtime"),
        ListenPort       = int.TryParse(Environment.GetEnvironmentVariable("ROVIA_LISTEN_PORT"), out int port) ? port : 2080,
        Mode             = Environment.GetEnvironmentVariable("ROVIA_MODE")?.Equals("tun", StringComparison.OrdinalIgnoreCase) == true
            ? SingBoxConnectionMode.Tun : SingBoxConnectionMode.SystemProxy
    };

    private static string DisplayName(ProxyNode node) => string.IsNullOrWhiteSpace(node.Name) ? node.Host : node.Name;
    private static string FormatMs(double? value) => value.HasValue ? $"{value:0.0} ms" : "unreachable";
    private static string FormatMetric(double? value, string unit) => value.HasValue ? $"{value:0.0} {unit}" : "unavailable";
    private static void RequireArguments(string[] args, int count, string message) { if (args.Length < count) throw new InvalidOperationException(message); }
    private static int Unknown(string command) { Console.Error.WriteLine($"error: unknown command '{command}'"); PrintUsage(); return 1; }

    private static void PrintUsage() => Console.WriteLine("""
        Rovia CLI
          rovia import <vless-link>
          rovia list
          rovia remove <node-id>
          rovia probe
          rovia rank
          rovia check-config <node-id>
          rovia connect <node-id>
          rovia connect-auto
          rovia status
          rovia disconnect
          rovia speed-test
          rovia diagnose

        Environment:
          ROVIA_DATA_DIR     Local state directory
          ROVIA_SING_BOX     sing-box executable path
          ROVIA_LISTEN_PORT  Local mixed proxy port (default: 2080)
        """);
}
