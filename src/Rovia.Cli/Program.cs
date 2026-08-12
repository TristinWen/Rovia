using Rovia.Backends.SingBox;
using Rovia.Config.Parsing;
using Rovia.Config.Storage;
using Rovia.Core.Engine;
using Rovia.Core.Failover;
using Rovia.Core.Health;
using Rovia.Core.Models;
using Rovia.Core.Policies;
using Rovia.Core.Routing;

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
        JsonNodeRepository repository = new(Path.Combine(dataDirectory, "nodes.json"));
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
                "check-config" => CheckConfig(args, repository, dataDirectory),
                _              => Unknown(args[0])
            };
        }
        catch (Exception exception) when (exception is ProxyLinkParseException or InvalidOperationException or IOException)
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
        await using AdaptiveRouteEngine engine = CreateEngine(repository, dataDirectory);
        ProxyNode node = automatic
            ? await engine.ConnectBestAsync()
            : repository.Get(args[1]) ?? throw new InvalidOperationException($"Node '{args[1]}' was not found.");
        if (!automatic)
            await engine.ConnectAsync(node);
        BackendStatus status = await engine.GetStatusAsync();
        Console.WriteLine($"connected {DisplayName(node)} via {status.LocalEndpoint}");
        Console.WriteLine("Press Ctrl+C to disconnect.");
        using CancellationTokenSource exit = new();
        Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; exit.Cancel(); };
        try { await Task.Delay(Timeout.InfiniteTimeSpan, exit.Token); } catch (OperationCanceledException) { }
        await engine.DisconnectAsync();
        Console.WriteLine("disconnected");
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

    private static AdaptiveRouteEngine CreateEngine(JsonNodeRepository repository, string dataDirectory)
    {
        SingBoxOptions options = CreateOptions(dataDirectory);
        return new(repository, new HealthMonitor(new TcpNodeProbe()), new RouteScorer(), new RouteSelector(),
            new FailoverEngine(), new SingBoxBackend(options, new SingBoxConfigBuilder()), new RoutingPolicy());
    }

    private static SingBoxOptions CreateOptions(string dataDirectory) => new()
    {
        ExecutablePath  = Environment.GetEnvironmentVariable("ROVIA_SING_BOX") ?? "sing-box",
        WorkingDirectory = Path.Combine(dataDirectory, "runtime"),
        ListenPort       = int.TryParse(Environment.GetEnvironmentVariable("ROVIA_LISTEN_PORT"), out int port) ? port : 2080
    };

    private static string DisplayName(ProxyNode node) => string.IsNullOrWhiteSpace(node.Name) ? node.Host : node.Name;
    private static string FormatMs(double? value) => value.HasValue ? $"{value:0.0} ms" : "unreachable";
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

        Environment:
          ROVIA_DATA_DIR     Local state directory
          ROVIA_SING_BOX     sing-box executable path
          ROVIA_LISTEN_PORT  Local mixed proxy port (default: 2080)
        """);
}
