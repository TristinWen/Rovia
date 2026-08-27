using System.IO.Compression;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace Rovia.Backends.Xray;

/// <summary>Installs an official Xray release after verifying its published SHA-256 digest.</summary>
public sealed class XrayProvisioner(HttpClient? httpClient = null)
{
    private static readonly Uri LatestReleaseUri = new("https://api.github.com/repos/XTLS/Xray-core/releases/latest");
    private readonly HttpClient _httpClient       = httpClient ?? CreateHttpClient();

    public async Task<string> EnsureAsync(string dataDirectory, CancellationToken cancellationToken = default)
    {
        string targetPath = Path.Combine(dataDirectory, "bin", OperatingSystem.IsWindows() ? "xray.exe" : "xray");
        if (File.Exists(targetPath))
            return targetPath;
        Release release    = await GetReleaseAsync(cancellationToken);
        string assetName   = $"Xray-{GetPlatform()}-{GetArchitecture()}.zip";
        Asset asset        = release.Assets.SingleOrDefault(item => item.Name.Equals(assetName, StringComparison.OrdinalIgnoreCase))
            ?? throw new PlatformNotSupportedException($"The latest Xray release has no asset named '{assetName}'.");
        byte[] archive     = await _httpClient.GetByteArrayAsync(asset.DownloadUrl, cancellationToken);
        string? expected   = asset.Digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true ? asset.Digest[7..] : null;
        if (expected is null)
            throw new InvalidDataException("The official Xray release asset does not provide a SHA-256 digest.");
        string actual = Convert.ToHexString(SHA256.HashData(archive));
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The downloaded Xray archive failed SHA-256 verification.");

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        string temporaryPath = $"{targetPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using MemoryStream stream = new(archive);
            using ZipArchive zip      = new(stream, ZipArchiveMode.Read);
            string executableName     = OperatingSystem.IsWindows() ? "xray.exe" : "xray";
            ZipArchiveEntry entry     = zip.Entries.SingleOrDefault(item => item.Name.Equals(executableName, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"The Xray archive does not contain {executableName}.");
            entry.ExtractToFile(temporaryPath, true);
            File.Move(temporaryPath, targetPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
        return targetPath;
    }

    private async Task<Release> GetReleaseAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(LatestReleaseUri, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Release>(cancellationToken)
            ?? throw new InvalidDataException("GitHub returned an invalid Xray release document.");
    }

    private static string GetPlatform() => OperatingSystem.IsWindows() ? "windows"
        : OperatingSystem.IsLinux() ? "linux"
        : OperatingSystem.IsMacOS() ? "macos" : throw new PlatformNotSupportedException();

    private static string GetArchitecture() => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X64   => "64",
        Architecture.X86   => "32",
        Architecture.Arm64 => "arm64-v8a",
        _ => throw new PlatformNotSupportedException($"Architecture {RuntimeInformation.ProcessArchitecture} is not supported.")
    };

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Rovia/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private sealed record Release([property: JsonPropertyName("assets")] IReadOnlyList<Asset> Assets);
    private sealed record Asset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] Uri DownloadUrl,
        [property: JsonPropertyName("digest")] string? Digest);
}
