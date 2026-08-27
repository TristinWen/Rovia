using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Rovia.Backends;
using Rovia.Backends.SingBox;
using Rovia.Backends.Xray;
using Rovia.Config.Parsing;
using Rovia.Config.Export;
using Rovia.Config.Storage;
using Rovia.Config.Subscriptions;
using Rovia.Core.Engine;
using Rovia.Core.Failover;
using Rovia.Core.Health;
using Rovia.Core.Models;
using Rovia.Core.Policies;
using Rovia.Core.Routing;
using Rovia.Platform.Windows.Proxy;
using Rovia.Platform.Windows.Security;
using Rovia.Runtime.Monitoring;
using Rovia.Runtime.Diagnostics;
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
        JsonSubscriptionStore subscriptions = new(Path.Combine(dataDirectory, "subscriptions.json"));
        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "import"       => Import(args, repository),
                "list"         => List(repository),
                "remove"       => Remove(args, repository),
                "subscription-add"     => await AddSubscriptionAsync(args, repository, subscriptions),
                "subscription-list"    => ListSubscriptions(subscriptions),
                "subscription-refresh" => await RefreshSubscriptionsAsync(args, repository, subscriptions),
                "subscription-remove"  => RemoveSubscription(args, repository, subscriptions),
                "probe"        => await ProbeAsync(repository),
                "rank"         => await RankAsync(repository),
                "connect"      => await LaunchHostAsync(args, dataDirectory, false),
                "connect-auto" => await LaunchHostAsync(args, dataDirectory, true),
                "host"         => await RunHostAsync(args, repository, dataDirectory, false),
                "host-auto"    => await RunHostAsync(args, repository, dataDirectory, true),
                "status"       => await StatusAsync(dataDirectory),
                "disconnect"   => await DisconnectAsync(dataDirectory),
                "speed-test"   => await SpeedTestAsync(dataDirectory),
                "diagnose"     => await DiagnoseAsync(dataDirectory),
                "network-diagnose" => await NetworkDiagnoseAsync(args, repository),
                "history"      => ShowHistory(dataDirectory),
                "export-diagnostics" => ExportDiagnostics(args, dataDirectory),
                "check-config" => CheckConfig(args, repository, dataDirectory),
                "xhttp-profiles" => GenerateXhttpProfiles(args, repository),
                _              => Unknown(args[0])
            };
        }
        catch (Exception exception) when (exception is ProxyLinkParseException or InvalidOperationException or IOException or
                                          HttpRequestException or TaskCanceledException or TimeoutException or Win32Exception)
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

    private static async Task<int> AddSubscriptionAsync(
        string[] args,
        JsonNodeRepository repository,
        JsonSubscriptionStore subscriptions)
    {
        RequireArguments(args, 3, "subscription-add requires a name and an HTTP(S) URL.");
        if (!Uri.TryCreate(args[2], UriKind.Absolute, out Uri? source) || source.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("Subscription URL must use HTTP or HTTPS.");
        SubscriptionDefinition definition = new()
        {
            Id     = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source.ToString())))[..24].ToLowerInvariant(),
            Name   = args[1],
            Source = source
        };
        subscriptions.Upsert(definition);
        SubscriptionImportResult result = await CreateSubscriptionService(repository, subscriptions).RefreshAsync(definition);
        Console.WriteLine($"added {definition.Id}\t{definition.Name}\t{result.Nodes.Count} nodes\t{result.Warnings.Count} warnings");
        return 0;
    }

    private static int ListSubscriptions(JsonSubscriptionStore subscriptions)
    {
        foreach (SubscriptionDefinition definition in subscriptions.GetAll())
            Console.WriteLine($"{definition.Id}\t{definition.Name}\t{(definition.Enabled ? "enabled" : "disabled")}\t{definition.LastRefreshedAt?.ToString("O") ?? "never"}\t{definition.LastError ?? "ok"}");
        return 0;
    }

    private static async Task<int> RefreshSubscriptionsAsync(
        string[] args,
        JsonNodeRepository repository,
        JsonSubscriptionStore subscriptions)
    {
        IReadOnlyList<SubscriptionDefinition> definitions = subscriptions.GetAll();
        if (args.Length >= 2)
            definitions = [definitions.FirstOrDefault(item => item.Id == args[1])
                ?? throw new InvalidOperationException($"Subscription '{args[1]}' was not found.")];
        SubscriptionRefreshService service = CreateSubscriptionService(repository, subscriptions);
        foreach (SubscriptionDefinition definition in definitions.Where(item => item.Enabled))
        {
            SubscriptionImportResult result = await service.RefreshAsync(definition);
            Console.WriteLine($"refreshed {definition.Id}\t{result.Nodes.Count} nodes\t{result.DuplicateCount} duplicates\t{result.Warnings.Count} warnings");
        }
        return 0;
    }

    private static int RemoveSubscription(
        string[] args,
        JsonNodeRepository repository,
        JsonSubscriptionStore subscriptions)
    {
        RequireArguments(args, 2, "subscription-remove requires a subscription identifier.");
        SubscriptionDefinition definition = subscriptions.GetAll().FirstOrDefault(item => item.Id == args[1])
            ?? throw new InvalidOperationException($"Subscription '{args[1]}' was not found.");
        repository.ReplaceSubscription(definition.Source.ToString(), []);
        subscriptions.Remove(definition.Id);
        Console.WriteLine($"removed {definition.Id}");
        return 0;
    }

    private static SubscriptionRefreshService CreateSubscriptionService(
        JsonNodeRepository repository,
        JsonSubscriptionStore subscriptions) => new(subscriptions, repository,
            new([new VlessLinkParser(), new TrojanLinkParser(), new VmessLinkParser(), new ShadowsocksLinkParser()]));

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

    private static async Task<int> LaunchHostAsync(string[] args, string dataDirectory, bool automatic)
    {
        if (!automatic)
            RequireArguments(args, 2, "connect requires a node identifier.");
        RuntimeState? current = new RuntimeStateStore(Path.Combine(dataDirectory, "runtime-state.json")).Read();
        if (current is { IsRunning: true })
            throw new InvalidOperationException("Rovia is already connected.");

        string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Unable to locate the Rovia executable.");
        ProcessStartInfo startInfo = new()
        {
            FileName               = executable,
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardError  = true,
            RedirectStandardOutput = true
        };
        if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
            startInfo.ArgumentList.Add(Assembly.GetEntryAssembly()?.Location
                ?? throw new InvalidOperationException("Unable to locate the Rovia CLI assembly."));
        startInfo.ArgumentList.Add(automatic ? "host-auto" : "host");
        if (!automatic)
            startInfo.ArgumentList.Add(args[1]);
        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start the Rovia background host.");
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(2);
        string statePath        = Path.Combine(dataDirectory, "runtime-state.json");
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (new RuntimeStateStore(statePath).Read() is { IsRunning: true } state)
            {
                Console.WriteLine($"connected {state.NodeName} via {state.LocalEndpoint}");
                return 0;
            }
            if (process.HasExited)
            {
                string error = await process.StandardError.ReadToEndAsync();
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                    ? "Rovia background host exited before becoming ready."
                    : error.Trim());
            }
            await Task.Delay(250);
        }
        throw new TimeoutException("Rovia background host did not become ready within two minutes.");
    }

    private static async Task<int> RunHostAsync(string[] args, JsonNodeRepository repository, string dataDirectory, bool automatic)
    {
        if (!automatic)
            RequireArguments(args, 2, "host requires a node identifier.");
        using Mutex runtimeMutex = new(false, $"Rovia.Runtime.{Environment.UserName}", out bool ownsRuntime);
        if (!ownsRuntime)
            throw new InvalidOperationException("Another Rovia runtime is already connected or connecting.");
        RuntimeLog log = new(Path.Combine(dataDirectory, "logs"));
        RuntimeLiveLogBuffer liveLogs = new();
        RecoverInterruptedRuntime(dataDirectory);
        SingBoxOptions requestedOptions = CreateOptions(dataDirectory);
        NetworkEnvironmentSnapshot environment = NetworkEnvironmentInspector.Capture();
        if (requestedOptions.Mode == SingBoxConnectionMode.Tun && environment.CloudflareWarpDetected)
        {
            throw new InvalidOperationException(
                "Cloudflare WARP is active. Use system-proxy mode so Rovia can share the WARP path without creating a conflicting second TUN interface.");
        }
        RuntimePreflight.EnsurePortAvailable(requestedOptions.ListenPort);
        await using AdaptiveRouteEngine engine = CreateEngine(repository, dataDirectory, liveLogs);
        IReadOnlyList<ProxyNode> candidates;
        if (automatic)
        {
            IReadOnlyList<RouteScore> scores = await engine.RankAsync();
            candidates = scores
                .Where(score => engine.GetHealth()[score.NodeId].State != NodeHealthState.Offline)
                .Select(score => repository.Get(score.NodeId)!)
                .ToArray();
        }
        else
        {
            ProxyNode primary = repository.Get(args[1]) ?? throw new InvalidOperationException($"Node '{args[1]}' was not found.");
            candidates = FallbackCandidateSelector.ForManualSelection(primary, repository.GetAll());
        }
        if (candidates.Count == 0)
            throw new InvalidOperationException("No TCP-reachable proxy node is available.");

        List<ProxyNode> expandedCandidates = [];
        foreach (ProxyNode candidate in candidates)
        {
            try
            {
                IPAddress[] addresses = await Dns.GetHostAddressesAsync(candidate.Host);
                expandedCandidates.AddRange(ResolvedEndpointCandidateFactory.Expand(candidate, addresses));
            }
            catch (Exception exception) when (exception is SocketException or ArgumentException)
            {
                expandedCandidates.Add(candidate);
            }
        }

        ProxyNode? node                         = null;
        BackendStatus? status                  = null;
        EgressVerificationResult? verification = null;
        List<string> failures                  = [];
        foreach (ProxyNode candidate in expandedCandidates)
        {
            try
            {
                await engine.ConnectAsync(candidate);
                BackendStatus candidateStatus = await engine.GetStatusAsync();
                EgressVerificationResult candidateVerification = await new MultiTargetEgressVerifier(new HttpEgressProbe()).VerifyAsync(candidateStatus.LocalEndpoint!);
                if (!candidateVerification.Success)
                {
                    failures.Add($"{DisplayName(candidate)}: {candidateVerification.Message}");
                    continue;
                }
                node         = candidate;
                status       = candidateStatus;
                verification = candidateVerification;
                break;
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or TimeoutException)
            {
                failures.Add($"{DisplayName(candidate)}: {exception.Message}");
            }
        }
        if (node is null || status is null || verification is null)
        {
            await engine.DisconnectAsync();
            throw new InvalidOperationException($"All configured transport candidates failed. {string.Join(" | ", failures)}");
        }
        EgressProbeResult egress = verification.FastestSuccess!;

        string statePath    = Path.Combine(dataDirectory, "runtime-state.json");
        string historyPath  = Path.Combine(dataDirectory, "route-history.json");
        string snapshotPath = Path.Combine(dataDirectory, "system-proxy.json");
        string pipeName     = $"rovia-{Environment.UserName}";
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        ProxyPerformance? performance = null;
        string runtimeMessage = $"{verification.Message} Fastest response: {egress.Target.Host} in {egress.Duration.TotalMilliseconds:0} ms.";
        using HttpProxyPerformanceProbe performanceProbe = new();
        RuntimeStateStore stateStore = new(statePath);
        using CancellationTokenSource exit = new();
        Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; exit.Cancel(); };
        RuntimeState State() => new()
        {
            IsRunning = !exit.IsCancellationRequested, ProcessId = Environment.ProcessId, BackendProcessId = status.ProcessId, NodeId = engine.CurrentNode?.Id,
            NodeName = engine.CurrentNode is null ? null : DisplayName(engine.CurrentNode), LocalEndpoint = status.LocalEndpoint?.ToString(),
            FailoverState = engine.FailoverState, StartedAt = startedAt, UpdatedAt = DateTimeOffset.UtcNow,
            LastMessage = performance?.Message ?? runtimeMessage,
            ProxyLatencyMs = performance?.LatencyMs, DownloadMbps = performance?.DownloadMbps, PerformanceAt = performance?.MeasuredAt,
            LiveLogs = liveLogs.Snapshot()
        };
        engine.RouteChanged += (_, eventArgs) =>
        {
            if (engine.LastSelectionDecision?.Reason is not { } reason)
                return;
            runtimeMessage = reason;
            stateStore.Write(State());
            log.Write("Information", "route.changed", $"{eventArgs.Previous?.Id ?? "none"} -> {eventArgs.Current.Id}: {reason}");
        };
        stateStore.Write(State());
        async Task MeasurePerformance(CancellationToken token)
        {
            performance = await performanceProbe.MeasureAsync(status.LocalEndpoint!, token);
            stateStore.Write(State());
        }
        RuntimeControlServer server = new(pipeName, State, exit.Cancel, MeasurePerformance);
        Task serverTask             = server.RunAsync(exit.Token);
        RouteEvaluationSignal evaluationSignal = new();
        AdaptiveRouteMonitor monitor = new(engine, new RouteHistoryStore(historyPath), TimeSpan.FromSeconds(30),
            new HealthHistoryStore(Path.Combine(dataDirectory, "health-history.json")), evaluationSignal);
        Task monitorTask             = automatic ? monitor.RunAsync(exit.Token) : Task.CompletedTask;
        Task subscriptionTask        = automatic ? RefreshSubscriptionsPeriodicallyAsync(repository, dataDirectory, log, exit.Token) : Task.CompletedTask;
        RuntimeLifetimeSupervisor supervisor = new(engine.GetStatusAsync, TimeSpan.FromSeconds(2));
        Task supervisorTask = supervisor.RunAsync(message =>
        {
            runtimeMessage = message;
            stateStore.Write(State());
            log.Write("Error", "runtime.backend-exited", message);
            exit.Cancel();
        }, exit.Token);
        SingBoxOptions activeOptions = CreateOptions(dataDirectory);
        IDisposable? proxyLease      = OperatingSystem.IsWindows() && activeOptions.Mode == SingBoxConnectionMode.SystemProxy
            ? SystemProxyLease.Activate(new WindowsSystemProxySettings(), snapshotPath, $"127.0.0.1:{activeOptions.ListenPort}")
            : null;
        log.Write("Information", "runtime.connected", $"Connected node {node.Id} on local port {activeOptions.ListenPort}.");
        NetworkAddressChangedEventHandler addressChanged = (_, _) => evaluationSignal.Pulse();
        NetworkAvailabilityChangedEventHandler availabilityChanged = (_, _) => evaluationSignal.Pulse();
        NetworkChange.NetworkAddressChanged  += addressChanged;
        NetworkChange.NetworkAvailabilityChanged += availabilityChanged;
        try { await Task.Delay(Timeout.InfiniteTimeSpan, exit.Token); }
        catch (OperationCanceledException) { }
        finally
        {
            NetworkChange.NetworkAddressChanged  -= addressChanged;
            NetworkChange.NetworkAvailabilityChanged -= availabilityChanged;
        }
        proxyLease?.Dispose();
        await engine.DisconnectAsync();
        stateStore.Write(State() with { IsRunning = false, LastMessage = "Disconnected cleanly." });
        log.Write("Information", "runtime.disconnected", "Disconnected cleanly and restored platform settings.");
        try { await Task.WhenAll(serverTask, monitorTask, subscriptionTask, supervisorTask); } catch (OperationCanceledException) { }
        return 0;
    }

    private static async Task RefreshSubscriptionsPeriodicallyAsync(
        JsonNodeRepository repository,
        string dataDirectory,
        RuntimeLog log,
        CancellationToken cancellationToken)
    {
        JsonSubscriptionStore subscriptions = new(Path.Combine(dataDirectory, "subscriptions.json"));
        SubscriptionRefreshService service   = CreateSubscriptionService(repository, subscriptions);
        using PeriodicTimer timer             = new(TimeSpan.FromMinutes(5));
        do
        {
            try
            {
                IReadOnlyList<SubscriptionImportResult> results = await service.RefreshDueAsync(DateTimeOffset.UtcNow, cancellationToken);
                if (results.Count > 0)
                    log.Write("Information", "subscriptions.refreshed", $"Refreshed {results.Count} due subscription providers.");
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException)
            {
                if (!cancellationToken.IsCancellationRequested)
                    log.Write("Warning", "subscriptions.refresh-failed", exception.Message);
            }
        }
        while (await timer.WaitForNextTickAsync(cancellationToken));
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

    private static async Task<int> NetworkDiagnoseAsync(string[] args, JsonNodeRepository repository)
    {
        ProxyNode? configured = args.Length < 2
            ? repository.GetAll().FirstOrDefault()
            : repository.GetAll().FirstOrDefault(node => node.Host.Equals(args[1], StringComparison.OrdinalIgnoreCase) &&
                                                         (args.Length < 3 || !int.TryParse(args[2], out int matchedPort) || node.Port == matchedPort));
        string host = args.Length >= 2 ? args[1] : configured?.Host
            ?? throw new InvalidOperationException("network-diagnose requires a host when no nodes are configured.");
        int port = args.Length >= 3 && int.TryParse(args[2], out int requestedPort) ? requestedPort : configured?.Port ?? 443;
        if (port is < 1 or > 65535)
            throw new InvalidOperationException("Diagnostic port must be between 1 and 65535.");

        NetworkEnvironmentSnapshot environment = NetworkEnvironmentInspector.Capture();
        Console.WriteLine($"cloudflare-warp={(environment.CloudflareWarpDetected ? "detected" : "not-detected")}");
        foreach (NetworkAdapterSnapshot adapter in environment.Adapters)
        {
            Console.WriteLine($"adapter\t{adapter.Name}\t{adapter.InterfaceType}\tmtu={adapter.Mtu?.ToString() ?? "unknown"}\twarp={adapter.IsCloudflareWarp}");
            Console.WriteLine($"  addresses={string.Join(',', adapter.Addresses)}");
            Console.WriteLine($"  dns={string.Join(',', adapter.DnsServers)}\tgateways={string.Join(',', adapter.Gateways)}");
        }
        if (OperatingSystem.IsWindows())
        {
            SystemProxySnapshot proxy = new WindowsSystemProxySettings().Read();
            Console.WriteLine($"windows-proxy\tenabled={proxy.Enabled}\tserver={proxy.Server ?? "none"}\tpac={(proxy.AutoConfigUrl is null ? "none" : "configured")}");
        }

        EndpointConnectivityResult endpoint = await new EndpointConnectivityProbe().ProbeAsync(host, port);
        Console.WriteLine($"endpoint\t{endpoint.Host}:{endpoint.Port}\ttls={(endpoint.TlsSucceeded ? "ok" : "failed")}\tprotocol={endpoint.TlsProtocol ?? "none"}");
        foreach (EndpointAddressResult address in endpoint.Addresses)
            Console.WriteLine($"  {address.Address}\ttcp={(address.TcpConnected ? "ok" : "failed")}\t{(address.TcpMilliseconds.HasValue ? $"{address.TcpMilliseconds:0.0} ms" : address.Error)}");
        if (endpoint.TlsSucceeded)
            Console.WriteLine($"  certificate-subject={endpoint.CertificateSubject}\n  certificate-issuer={endpoint.CertificateIssuer}");
        else
            Console.WriteLine($"  tls-error={endpoint.TlsError}");
        bool websocketSucceeded = true;
        if (configured?.Transport?.Type.Equals("ws", StringComparison.OrdinalIgnoreCase) == true)
        {
            WebSocketConnectivityResult websocket = await new WebSocketConnectivityProbe().ProbeAsync(
                configured.Host,
                configured.Port,
                configured.Tls?.Enabled == true,
                configured.Transport.Path,
                configured.Transport.Host);
            websocketSucceeded = websocket.Success;
            Console.WriteLine($"websocket\t{websocket.Target}\tupgrade={(websocket.Success ? "ok" : "failed")}\t{websocket.DurationMilliseconds:0.0} ms");
            if (!websocket.Success)
                Console.WriteLine($"  websocket-error={websocket.Error}");
        }
        return endpoint.Addresses.Any(address => address.TcpConnected) && endpoint.TlsSucceeded && websocketSucceeded ? 0 : 2;
    }

    private static int ShowHistory(string dataDirectory)
    {
        foreach (RouteChangeRecord record in new RouteHistoryStore(Path.Combine(dataDirectory, "route-history.json")).Read().TakeLast(20))
            Console.WriteLine($"route\t{record.At:O}\t{record.PreviousNodeId ?? "-"}\t{record.CurrentNodeId}\t{record.Reason}");
        foreach (NodeHealth sample in new HealthHistoryStore(Path.Combine(dataDirectory, "health-history.json")).Read().TakeLast(20))
            Console.WriteLine($"health\t{sample.LastCheckedAt:O}\t{sample.NodeId}\t{sample.State}\tlatency={FormatMetric(sample.LatencyMs, "ms")}\tsuccess={sample.SuccessRate:P0}\tfailures={sample.ConsecutiveFailures}");
        return 0;
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
        string json = node.Transport?.Type.Equals("xhttp", StringComparison.OrdinalIgnoreCase) == true
            ? new XrayConfigBuilder().Build(node, CreateXrayOptions(dataDirectory))
            : new SingBoxConfigBuilder().Build(node, CreateOptions(dataDirectory));
        Console.WriteLine(json);
        return 0;
    }

    private static int GenerateXhttpProfiles(string[] args, JsonNodeRepository repository)
    {
        RequireArguments(args, 2, "xhttp-profiles requires a node identifier.");
        ProxyNode node = repository.Get(args[1]) ?? throw new InvalidOperationException($"Node '{args[1]}' was not found.");
        foreach ((string name, string link) in new XhttpProfileGenerator().Generate(node))
            Console.WriteLine($"{name}: {link}");
        return 0;
    }

    private static int ExportDiagnostics(string[] args, string dataDirectory)
    {
        string destination = args.Length >= 2
            ? args[1]
            : Path.Combine(Environment.CurrentDirectory, $"rovia-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        Console.WriteLine(DiagnosticExporter.Export(dataDirectory, destination));
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

    private static AdaptiveRouteEngine CreateEngine(JsonNodeRepository repository, string dataDirectory, RuntimeLiveLogBuffer? liveLogs = null)
    {
        SingBoxOptions options = CreateOptions(dataDirectory);
        return new(repository, new HealthMonitor(new TcpNodeProbe()), new RouteScorer(), new RouteSelector(),
            new FailoverEngine(), new ProxyBackendRouter(dataDirectory, options, CreateXrayOptions(dataDirectory),
                liveLogs is null ? null : liveLogs.Add), new RoutingPolicy());
    }

    private static SingBoxOptions CreateOptions(string dataDirectory, string? singBoxPath = null)
    {
        RoutingConfiguration routing = new JsonRoutingConfigurationStore(Path.Combine(dataDirectory, "routing.json")).Read();
        return new()
        {
            ExecutablePath   = singBoxPath ?? Environment.GetEnvironmentVariable("ROVIA_SING_BOX") ?? "sing-box",
            WorkingDirectory = Path.Combine(dataDirectory, "runtime"),
            ListenPort       = int.TryParse(Environment.GetEnvironmentVariable("ROVIA_LISTEN_PORT"), out int port) ? port : 2080,
            Mode             = Environment.GetEnvironmentVariable("ROVIA_MODE")?.Equals("tun", StringComparison.OrdinalIgnoreCase) == true
                ? SingBoxConnectionMode.Tun : SingBoxConnectionMode.SystemProxy,
            RoutingRules     = routing.Rules,
            DnsPolicy        = routing.Dns,
            LogLevel         = Environment.GetEnvironmentVariable("ROVIA_LOG_LEVEL") ?? "info",
        };
    }

    private static XrayOptions CreateXrayOptions(string dataDirectory) => new()
    {
        ExecutablePath   = Environment.GetEnvironmentVariable("ROVIA_XRAY") ?? "xray",
        WorkingDirectory = Path.Combine(dataDirectory, "runtime"),
        ListenPort       = int.TryParse(Environment.GetEnvironmentVariable("ROVIA_LISTEN_PORT"), out int port) ? port : 2080,
        LogLevel         = Environment.GetEnvironmentVariable("ROVIA_LOG_LEVEL") ?? "warning"
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
          rovia subscription-add <name> <url>
          rovia subscription-list
          rovia subscription-refresh [subscription-id]
          rovia subscription-remove <subscription-id>
          rovia probe
          rovia rank
          rovia check-config <node-id>
          rovia xhttp-profiles <node-id>
          rovia connect <node-id>
          rovia connect-auto
          rovia status
          rovia disconnect
          rovia speed-test
          rovia diagnose
          rovia network-diagnose [host] [port]
          rovia history
          rovia export-diagnostics [output.zip]

        Environment:
          ROVIA_DATA_DIR     Local state directory
          ROVIA_SING_BOX     sing-box executable path
          ROVIA_LISTEN_PORT  Local mixed proxy port (default: 2080)
        """);
}
