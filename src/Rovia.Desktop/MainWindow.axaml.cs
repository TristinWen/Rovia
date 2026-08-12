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
                UseShellExecute = false,
                CreateNoWindow  = true
            };
            startInfo.Environment["ROVIA_DATA_DIR"] = _dataDirectory;
            startInfo.Environment["ROVIA_SING_BOX"] = FindSingBoxPath();
            Process.Start(startInfo);
            MessageText.Text = "Connecting and verifying proxy egress…";
            await Task.Delay(3500);
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

    private async void RefreshClicked(object? sender, RoutedEventArgs eventArgs) => await RefreshStatusAsync();

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
    }

    private void ReloadNodes()
    {
        _nodes.Clear();
        foreach (ProxyNode node in _repository.GetAll())
            _nodes.Add(new(DisplayName(node), $"{node.Host}:{node.Port}", node.Protocol.ToString()));
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

    private static string FindSingBoxPath()
    {
        string? configured = Environment.GetEnvironmentVariable("ROVIA_SING_BOX");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;
        string v2rayPath = @"D:\Softwares\v2rayN\bin\sing_box\sing-box.exe";
        return File.Exists(v2rayPath) ? v2rayPath : "sing-box";
    }

    private static string DisplayName(ProxyNode node) => string.IsNullOrWhiteSpace(node.Name) ? node.Host : node.Name;

    private sealed record NodeItem(string Name, string Endpoint, string Protocol);
}
