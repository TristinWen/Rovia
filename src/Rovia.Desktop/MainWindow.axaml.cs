using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Rovia.Config.Parsing;
using Rovia.Config.Storage;
using Rovia.Core.Models;
using Rovia.Runtime.Runtime;

namespace Rovia.Desktop;

/// <summary>Provides a thin desktop shell over Rovia configuration and runtime control.</summary>
public partial class MainWindow : Window
{
    private readonly string _dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rovia");
    private readonly ObservableCollection<NodeItem> _nodes = [];
    private readonly JsonNodeRepository _repository;

    public MainWindow()
    {
        InitializeComponent();
        _repository          = new(Path.Combine(_dataDirectory, "nodes.json"));
        NodeList.ItemsSource = _nodes;
        ReloadNodes();
        Opened += async (_, _) => await RefreshStatusAsync();
    }

    private void ImportClicked(object? sender, RoutedEventArgs eventArgs)
    {
        try
        {
            ProxyNode node = new VlessLinkParser().Parse(LinkTextBox.Text ?? string.Empty);
            _repository.Add(node);
            LinkTextBox.Clear();
            ReloadNodes();
            MessageText.Text = $"Imported {DisplayName(node)}.";
        }
        catch (Exception exception) when (exception is ProxyLinkParseException or IOException)
        {
            MessageText.Text = exception.Message;
        }
    }

    private async void ConnectClicked(object? sender, RoutedEventArgs eventArgs)
    {
        try
        {
            RuntimeState? state = new RuntimeStateStore(Path.Combine(_dataDirectory, "runtime-state.json")).Read();
            if (state is { IsRunning: true })
                throw new InvalidOperationException("Rovia is already connected.");
            string cliPath = FindCliPath();
            ProcessStartInfo startInfo = new(cliPath, "connect-auto")
            {
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardError  = true,
                RedirectStandardOutput = true
            };
            startInfo.Environment["ROVIA_DATA_DIR"] = _dataDirectory;
            Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start the Rovia runtime.");
            MessageText.Text = "Preparing sing-box and verifying proxy egress…";
            await WaitForRuntimeAsync(process);
            await RefreshStatusAsync();
        }
        catch (Exception exception)
        {
            MessageText.Text = exception.Message;
        }
    }

    private async void DisconnectClicked(object? sender, RoutedEventArgs eventArgs)
    {
        try
        {
            await new RuntimeControlClient($"rovia-{Environment.UserName}").SendAsync("disconnect");
            MessageText.Text = "Disconnect requested.";
            await Task.Delay(500);
            await RefreshStatusAsync();
        }
        catch (Exception exception)
        {
            MessageText.Text = exception.Message;
        }
    }

    private void DeleteClicked(object? sender, RoutedEventArgs eventArgs)
    {
        if (NodeList.SelectedItem is not NodeItem selected)
        {
            MessageText.Text = "Select a node to delete.";
            return;
        }
        _repository.Remove(selected.Id);
        ReloadNodes();
        MessageText.Text = $"Deleted {selected.Name}.";
    }

    private async void RefreshClicked(object? sender, RoutedEventArgs eventArgs) => await RefreshStatusAsync();

    private async void SpeedTestClicked(object? sender, RoutedEventArgs eventArgs)
    {
        try
        {
            MessageText.Text = "Measuring warmed latency and up to 5 MB download throughput…";
            RuntimeState state = await new RuntimeControlClient($"rovia-{Environment.UserName}").SendAsync("speed-test", TimeSpan.FromSeconds(30));
            ShowPerformance(state);
            MessageText.Text = state.LastMessage;
        }
        catch (Exception exception)
        {
            MessageText.Text = exception.Message;
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
        StatusText.Text  = state is { IsRunning: true } ? $"Connected · {state.NodeName} · {state.LocalEndpoint}" : "Disconnected";
        MessageText.Text = state?.LastMessage ?? "Ready.";
        ShowPerformance(state);
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
            if (process.HasExited)
            {
                string error  = await process.StandardError.ReadToEndAsync();
                string output = await process.StandardOutput.ReadToEndAsync();
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output.Trim() : error.Trim());
            }
            if (new RuntimeStateStore(statePath).Read() is { IsRunning: true })
                return;
            await Task.Delay(500);
        }
        throw new TimeoutException("Rovia did not finish preparing sing-box within two minutes.");
    }

    private void ReloadNodes()
    {
        _nodes.Clear();
        foreach (ProxyNode node in _repository.GetAll())
            _nodes.Add(new(node.Id, DisplayName(node), $"{node.Host}:{node.Port}", node.Protocol.ToString()));
    }

    private static string FindCliPath()
    {
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

    private sealed record NodeItem(string Id, string Name, string Endpoint, string Protocol);
}
