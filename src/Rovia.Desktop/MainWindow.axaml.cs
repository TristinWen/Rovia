using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Rovia.Config.Parsing;
using Rovia.Config.Storage;
using Rovia.Config.Subscriptions;
using Rovia.Core.Models;
using Rovia.Platform.Windows.Security;
using Rovia.Runtime.Runtime;

namespace Rovia.Desktop;

/// <summary>Provides a thin desktop shell over Rovia configuration and runtime control.</summary>
public partial class MainWindow : Window
{
    private static readonly IBrush ConnectedBrush    = new SolidColorBrush(Color.Parse("#22C55E"));
    private static readonly IBrush DisconnectedBrush = new SolidColorBrush(Color.Parse("#EF4444"));

    private readonly string _dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rovia");
    private readonly ObservableCollection<NodeItem> _nodes = [];
    private readonly ObservableCollection<LogEntry> _log = [];
    private readonly JsonNodeRepository _repository;
    private bool _speedTestRunning;
    private Process? _runtimeProcess;

    public MainWindow()
    {
        InitializeComponent();
        _repository          = new(Path.Combine(_dataDirectory, "nodes.json"), new WindowsCredentialProtector());
        NodeList.ItemsSource = _nodes;
        LogList.ItemsSource   = _log;
        ReloadNodes();
        AppendLog("INFO", "Application started.");
        Opened += async (_, _) => await RefreshStatusAsync();
    }

    private async void ImportLinkClicked(object? sender, RoutedEventArgs eventArgs)
    {
        string? link = await InputDialog.ShowAsync(this, "Import link", "Paste a VLESS share link");
        if (string.IsNullOrWhiteSpace(link))
            return;
        try
        {
            ProxyNode node = new VlessLinkParser().Parse(link);
            _repository.Add(node);
            ReloadNodes();
            SetMessage($"Imported {DisplayName(node)}.");
            AppendLog("INFO", $"Imported node {DisplayName(node)} ({node.Host}:{node.Port}).");
        }
        catch (Exception exception) when (exception is ProxyLinkParseException or IOException)
        {
            SetMessage(exception.Message);
            AppendLog("ERROR", $"Import failed: {exception.Message}");
        }
    }

    private async void ImportSubscriptionMenuItemClicked(object? sender, RoutedEventArgs eventArgs)
    {
        string? url = await InputDialog.ShowAsync(this, "Import subscription", "Subscription URL");
        if (string.IsNullOrWhiteSpace(url))
            return;
        await ImportSubscriptionAsync(url);
    }

    private async Task ImportSubscriptionAsync(string url)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? source) || source.Scheme is not ("http" or "https"))
                throw new InvalidOperationException("Enter a valid HTTP or HTTPS subscription URL.");
            AppendLog("INFO", $"Fetching subscription {source}...");
            SubscriptionImporter importer = new([new VlessLinkParser(), new TrojanLinkParser(), new VmessLinkParser(), new ShadowsocksLinkParser()]);
            SubscriptionImportResult result = await importer.ImportAsync(source);
            foreach (ProxyNode node in result.Nodes)
                _repository.Add(node);
            ReloadNodes();
            SetMessage($"Imported {result.Nodes.Count} nodes; skipped {result.DuplicateCount} duplicates and {result.Warnings.Count} invalid entries.");
            AppendLog("INFO", $"Subscription import done: {result.Nodes.Count} added, {result.DuplicateCount} duplicates, {result.Warnings.Count} invalid.");
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or IOException)
        {
            SetMessage(exception.Message);
            AppendLog("ERROR", $"Subscription import failed: {exception.Message}");
        }
    }

    private void ClearLogClicked(object? sender, RoutedEventArgs eventArgs)
    {
        _log.Clear();
        AppendLog("INFO", "Log cleared.");
    }

    private void ExitClicked(object? sender, RoutedEventArgs eventArgs) => Close();

    private async void ConnectBestClicked(object? sender, RoutedEventArgs eventArgs) => await ConnectAsync("connect-auto");

    private async void ConnectSelectedClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (NodeList.SelectedItem is not NodeItem selected)
        {
            SetMessage("Select a node to connect.");
            return;
        }
        AppendLog("INFO", $"Connecting to selected node {selected.Name} ({selected.Host}:{selected.Port})...");
        await ConnectAsync($"connect {selected.Id}");
    }

    private async Task ConnectAsync(string command)
    {
        try
        {
            RuntimeState? state = new RuntimeStateStore(Path.Combine(_dataDirectory, "runtime-state.json")).Read();
            if (state is { IsRunning: true })
                throw new InvalidOperationException("Rovia is already connected.");
            string cliPath = FindCliPath();
            AppendLog("INFO", $"Starting runtime: {cliPath} {command}");
            ProcessStartInfo startInfo = new(cliPath, command)
            {
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardError  = true,
                RedirectStandardOutput = true
            };
            startInfo.Environment["ROVIA_DATA_DIR"] = _dataDirectory;
            startInfo.Environment["ROVIA_MODE"]     = (ModeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "system-proxy";
            Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start the Rovia runtime.");
            _runtimeProcess = process;
            _ = Task.Run(() => ReadRuntimeOutputStreamAsync(process.StandardOutput, "INFO"));
            _ = Task.Run(() => ReadRuntimeOutputStreamAsync(process.StandardError, "ERROR"));
            SetMessage("Preparing sing-box and verifying proxy egress...");
            await WaitForRuntimeAsync(process);
            await RefreshStatusAsync();
        }
        catch (Exception exception)
        {
            SetMessage(exception.Message);
            AppendLog("ERROR", $"Connect failed: {exception.Message}");
        }
    }

    private async void DisconnectClicked(object? sender, RoutedEventArgs eventArgs)
    {
        try
        {
            AppendLog("INFO", "Sending disconnect request to runtime...");
            await new RuntimeControlClient($"rovia-{Environment.UserName}").SendAsync("disconnect");
            SetMessage("Disconnect requested.");
            await Task.Delay(500);
            await RefreshStatusAsync();
        }
        catch (Exception exception)
        {
            SetMessage(exception.Message);
            AppendLog("ERROR", $"Disconnect failed: {exception.Message}");
        }
    }

    private void DeleteClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (NodeList.SelectedItem is not NodeItem selected)
        {
            SetMessage("Select a node to delete.");
            return;
        }
        _repository.Remove(selected.Id);
        ReloadNodes();
        SetMessage($"Deleted {selected.Name}.");
        AppendLog("INFO", $"Deleted node {selected.Name}.");
    }

    private async void RefreshClicked(object? sender, RoutedEventArgs eventArgs) => await RefreshStatusAsync();

    private async void SpeedTestClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (_speedTestRunning)
        {
            SetMessage("A speed test is already running.");
            return;
        }
        try
        {
            _speedTestRunning = true;
            SetMessage("Quick test: warming connection and sampling up to 512 KB for 3 seconds...");
            AppendLog("INFO", "Starting speed test...");
            RuntimeState state = await new RuntimeControlClient($"rovia-{Environment.UserName}").SendAsync("speed-test", TimeSpan.FromSeconds(30));
            ShowPerformance(state);
            SetMessage(state.LastMessage);
            AppendLog("INFO", $"Speed test finished: {state.LastMessage}");
        }
        catch (Exception exception)
        {
            SetMessage(exception.Message);
            AppendLog("ERROR", $"Speed test failed: {exception.Message}");
        }
        finally
        {
            _speedTestRunning = false;
        }
    }

    private async void ProbeClicked(object? sender, RoutedEventArgs eventArgs) => await RunCliCommandAsync("rank", "Probing all nodes...");

    private async void DiagnoseClicked(object? sender, RoutedEventArgs eventArgs)
    {
        RuntimeState? state = new RuntimeStateStore(Path.Combine(_dataDirectory, "runtime-state.json")).Read();
        if (state is { IsRunning: true })
        {
            await RunCliCommandAsync("diagnose", "Running DNS and egress diagnostics...");
            return;
        }
        if (NodeList.SelectedItem is not NodeItem selected)
        {
            SetMessage("Select a node to diagnose before connecting.");
            return;
        }
        await RunCliCommandAsync($"network-diagnose \"{selected.Host}\" {selected.Port}", "Testing DNS, TCP, TLS, and configured transport...");
    }

    private async void ExportDiagnosticsClicked(object? sender, RoutedEventArgs eventArgs)
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string output  = Path.Combine(desktop, $"rovia-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        await RunCliCommandAsync($"export-diagnostics \"{output}\"", "Creating a redacted support archive...");
    }

    private async Task RunCliCommandAsync(string command, string progress)
    {
        try
        {
            SetMessage(progress);
            AppendLog("INFO", progress);
            ProcessStartInfo startInfo = new(FindCliPath(), command) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            startInfo.Environment["ROVIA_DATA_DIR"] = _dataDirectory;
            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start the Rovia command.");
            string output = await process.StandardOutput.ReadToEndAsync();
            string error  = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            string result = string.IsNullOrWhiteSpace(error) ? output.Trim().Replace(Environment.NewLine, " · ") : error.Trim();
            SetMessage(result);
            AppendLog(string.IsNullOrWhiteSpace(error) ? "INFO" : "ERROR", result);
        }
        catch (Exception exception)
        {
            SetMessage(exception.Message);
            AppendLog("ERROR", exception.Message);
        }
    }

    private async Task RefreshStatusAsync()
    {
        RuntimeState? state = new RuntimeStateStore(Path.Combine(_dataDirectory, "runtime-state.json")).Read();
        if (state is { IsRunning: true })
        {
            try { state = await new RuntimeControlClient($"rovia-{Environment.UserName}").SendAsync("status"); }
            catch (IOException) { }
        }
        UpdateStatusIndicator(state);
        SetMessage(state?.LastMessage ?? "Ready.");
        ShowPerformance(state);
    }

    private void UpdateStatusIndicator(RuntimeState? state)
    {
        if (state is { IsRunning: true })
        {
            StatusDot.Fill       = ConnectedBrush;
            StatusText.Text       = $"Connected · {state.NodeName} · {state.LocalEndpoint}";
            StatusText.Foreground = ConnectedBrush;
            AppendLog("INFO", $"Connected to {state.NodeName} via {state.LocalEndpoint}.");
        }
        else
        {
            StatusDot.Fill       = DisconnectedBrush;
            StatusText.Text       = "Disconnected";
            StatusText.Foreground = DisconnectedBrush;
            if (state?.LastMessage is { } message)
                AppendLog("WARN", $"Disconnected: {message}");
        }
    }

    private void ShowPerformance(RuntimeState? state)
    {
        string latency = state?.ProxyLatencyMs is double latencyValue ? $"{latencyValue:0.0} ms" : "—";
        string speed   = state?.DownloadMbps is double speedValue ? $"{speedValue:0.0} Mbps" : "—";
        PerformanceText.Text = $"Proxy latency {latency} · Download {speed}";
    }

    private async Task WaitForRuntimeAsync(Process process)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(2);
        string statePath        = Path.Combine(_dataDirectory, "runtime-state.json");
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (new RuntimeStateStore(statePath).Read() is { IsRunning: true })
            {
                AppendLog("INFO", "Runtime reported running.");
                return;
            }
            if (process.HasExited)
            {
                string error  = await process.StandardError.ReadToEndAsync();
                string output = await process.StandardOutput.ReadToEndAsync();
                AppendLog("ERROR", $"Runtime exited: {error.Trim()}");
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output.Trim() : error.Trim());
            }
            await Task.Delay(500);
        }
        AppendLog("ERROR", "Runtime did not become ready within two minutes.");
        throw new TimeoutException("Rovia did not finish preparing sing-box within two minutes.");
    }

    private async Task ReadRuntimeOutputStreamAsync(StreamReader reader, string level)
    {
        try
        {
            string? line;
            while ((line = await reader.ReadLineAsync()) is not null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    AppendLog(level, line.Trim());
            }
        }
        catch (ObjectDisposedException) { }
        catch (IOException) { }
    }

    private void ReloadNodes()
    {
        _nodes.Clear();
        foreach (ProxyNode node in _repository.GetAll())
            _nodes.Add(new(node.Id, DisplayName(node), $"{node.Host}:{node.Port}", node.Protocol.ToString(), node.Host, node.Port));
        AppendLog("INFO", $"Loaded {_nodes.Count} node(s).");
    }

    private void SetMessage(string? message) => MessageText.Text = message ?? string.Empty;

    private void AppendLog(string level, string message)
    {
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => AppendLog(level, message));
            return;
        }
        string time = DateTime.Now.ToString("HH:mm:ss");
        _log.Add(new LogEntry(time, level, message));
        while (_log.Count > 2000)
            _log.RemoveAt(0);
        if (_log.Count > 0)
            LogList.ScrollIntoView(_log[^1]);
    }

    private static string FindCliPath()
    {
        string published = Path.Combine(AppContext.BaseDirectory, "runtime", "rovia.exe");
        if (File.Exists(published))
            return published;
        string sibling = Path.Combine(AppContext.BaseDirectory, "rovia.exe");
        if (File.Exists(sibling))
            return sibling;
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "src", "Rovia.Cli", "bin", "Release", "net8.0", "rovia.exe");
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Build Rovia.Cli in Release mode or place rovia.exe beside Rovia.Desktop.");
    }

    private static string DisplayName(ProxyNode node) => string.IsNullOrWhiteSpace(node.Name) ? node.Host : node.Name;

    private sealed record NodeItem(string Id, string Name, string Endpoint, string Protocol, string Host, int Port);

    private sealed record LogEntry(string Time, string Level, string Message);
}
