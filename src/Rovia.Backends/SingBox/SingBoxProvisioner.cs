using System.IO.Compression;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rovia.Backends.SingBox;

/// <summary>Locates or securely installs the official sing-box binary for the current platform.</summary>
public sealed class SingBoxProvisioner(HttpClient? httpClient = null)
{
    private static readonly Uri LatestReleaseUri = new("https://api.github.com/repos/SagerNet/sing-box/releases/latest");
    private readonly HttpClient _httpClient = httpClient ?? CreateHttpClient();

    public async Task<string> EnsureAsync(string dataDirectory, CancellationToken cancellationToken = default)
    {
        string? configured = Environment.GetEnvironmentVariable("ROVIA_SING_BOX");
        if (!string.IsNullOrWhiteSpace(configured))
            return File.Exists(configured) ? Path.GetFullPath(configured) : throw new FileNotFoundException("ROVIA_SING_BOX does not point to an existing file.", configured);

        string targetPath = Path.Combine(dataDirectory, "bin", OperatingSystem.IsWindows() ? "sing-box.exe" : "sing-box");
        if (File.Exists(targetPath))
            return targetPath;

        Release release = await GetReleaseAsync(cancellationToken);
        string platform = GetPlatform();
        string architecture = GetArchitecture();
        string extension = OperatingSystem.IsWindows() ? ".zip" : ".tar.gz";
        string expectedName = $"sing-box-{release.TagName.TrimStart('v')}-{platform}-{architecture}{extension}";
        Asset asset = release.Assets.SingleOrDefault(item => item.Name.Equals(expectedName, StringComparison.Ordinal))
            ?? throw new PlatformNotSupportedException($"The latest sing-box release has no asset named '{expectedName}'.");
        if (string.IsNullOrWhiteSpace(asset.Digest) || !asset.Digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The official release asset does not provide a SHA-256 digest.");

        byte[] archive = await _httpClient.GetByteArrayAsync(asset.DownloadUrl, cancellationToken);
        string actualDigest = Convert.ToHexString(SHA256.HashData(archive));
        if (!actualDigest.Equals(asset.Digest[7..], StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The downloaded sing-box archive failed SHA-256 verification.");

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        string temporaryPath = $"{targetPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            ExtractExecutable(archive, temporaryPath);
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
            ?? throw new InvalidDataException("GitHub returned an invalid sing-box release document.");
    }

    private static void ExtractExecutable(byte[] archive, string destination)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Automatic extraction currently supports Windows packages only.");
        using MemoryStream stream = new(archive);
        using ZipArchive zip = new(stream, ZipArchiveMode.Read);
        ZipArchiveEntry entry = zip.Entries.SingleOrDefault(item => item.Name.Equals("sing-box.exe", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("The sing-box archive does not contain sing-box.exe.");
        entry.ExtractToFile(destination, true);
    }

    private static string GetPlatform() => OperatingSystem.IsWindows() ? "windows"
        : OperatingSystem.IsLinux() ? "linux"
        : OperatingSystem.IsMacOS() ? "darwin"
        : throw new PlatformNotSupportedException("The current operating system is not supported by sing-box provisioning.");

    private static string GetArchitecture() => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X64   => "amd64",
        Architecture.X86   => "386",
        Architecture.Arm64 => "arm64",
        _ => throw new PlatformNotSupportedException($"Architecture {RuntimeInformation.ProcessArchitecture} is not supported.")
    };

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Rovia/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private sealed record Release(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("assets")] IReadOnlyList<Asset> Assets);

    private sealed record Asset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] Uri DownloadUrl,
        [property: JsonPropertyName("digest")] string? Digest);
}
