using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Rovia.Backends.Xray;

/// <summary>Installs an official Xray release after verifying its published SHA-256 digest.</summary>
public sealed class XrayProvisioner(HttpClient? httpClient = null)
{
    private static readonly Uri LatestReleaseUri = new("https://github.com/XTLS/Xray-core/releases/latest/download/");
    private readonly HttpClient _httpClient      = httpClient ?? CreateHttpClient();

    public async Task<string> EnsureAsync(string dataDirectory, CancellationToken cancellationToken = default)
    {
        string targetPath = Path.Combine(dataDirectory, "bin", OperatingSystem.IsWindows() ? "xray.exe" : "xray");
        if (File.Exists(targetPath))
            return targetPath;
        string assetName = $"Xray-{GetPlatform()}-{GetArchitecture()}.zip";
        byte[] archive   = await _httpClient.GetByteArrayAsync(new Uri(LatestReleaseUri, assetName), cancellationToken);
        byte[] digest    = await _httpClient.GetByteArrayAsync(new Uri(LatestReleaseUri, $"{assetName}.dgst"), cancellationToken);
        Match match      = Regex.Match(Encoding.UTF8.GetString(digest), @"^SHA2-256=\s*([0-9a-f]{64})\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        if (!match.Success)
            throw new InvalidDataException("The official Xray digest file does not contain SHA2-256.");
        string expected = match.Groups[1].Value;
        string actual   = Convert.ToHexString(SHA256.HashData(archive));
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
}
