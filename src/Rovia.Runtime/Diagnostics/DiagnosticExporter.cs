using System.IO.Compression;
using System.Text.Json;
using Rovia.Runtime.Runtime;

namespace Rovia.Runtime.Diagnostics;

/// <summary>Exports redacted runtime state and logs without node configuration or credentials.</summary>
public static class DiagnosticExporter
{
    public static string Export(string dataDirectory, string destinationPath)
    {
        string fullDestination = Path.GetFullPath(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullDestination)!);
        using FileStream stream = File.Create(fullDestination);
        using ZipArchive archive = new(stream, ZipArchiveMode.Create);
        AddEnvironment(archive);
        AddState(archive, Path.Combine(dataDirectory, "runtime-state.json"));
        AddLogs(archive, Path.Combine(dataDirectory, "logs"));
        return fullDestination;
    }

    private static void AddEnvironment(ZipArchive archive)
    {
        ZipArchiveEntry entry = archive.CreateEntry("environment.json");
        using StreamWriter writer = new(entry.Open());
        writer.Write(JsonSerializer.Serialize(new
        {
            ExportedAt = DateTimeOffset.UtcNow,
            OS         = Environment.OSVersion.VersionString,
            Runtime    = Environment.Version.ToString(),
            Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString()
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void AddState(ZipArchive archive, string statePath)
    {
        RuntimeState? state = new RuntimeStateStore(statePath).Read();
        if (state is null)
            return;
        ZipArchiveEntry entry = archive.CreateEntry("runtime-state.json");
        using StreamWriter writer = new(entry.Open());
        writer.Write(JsonSerializer.Serialize(state with { NodeId = null, NodeName = null }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void AddLogs(ZipArchive archive, string logDirectory)
    {
        if (!Directory.Exists(logDirectory))
            return;
        foreach (string path in Directory.EnumerateFiles(logDirectory, "rovia.jsonl*"))
            archive.CreateEntryFromFile(path, $"logs/{Path.GetFileName(path)}");
    }
}
