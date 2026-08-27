using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Rovia.Backends.Xray;

namespace Rovia.Backends.Tests;

/// <summary>Validates quota-free official Xray installation and digest verification.</summary>
public sealed class XrayProvisionerTests
{
    [Fact]
    public async Task EnsureAsync_DownloadsLatestAssetWithoutGithubApi()
    {
        byte[] archive = CreateArchive();
        string digest  = Convert.ToHexString(SHA256.HashData(archive));
        HttpClient client = new(new StubHandler(archive, Encoding.UTF8.GetBytes($"SHA2-256= {digest}")));
        string directory  = Path.Combine(Path.GetTempPath(), $"rovia-xray-{Guid.NewGuid():N}");
        try
        {
            string path = await new XrayProvisioner(client).EnsureAsync(directory);

            Assert.Equal("binary", await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private static byte[] CreateArchive()
    {
        using MemoryStream stream = new();
        using (ZipArchive zip = new(stream, ZipArchiveMode.Create, true))
        {
            ZipArchiveEntry entry = zip.CreateEntry(OperatingSystem.IsWindows() ? "xray.exe" : "xray");
            using StreamWriter writer = new(entry.Open());
            writer.Write("binary");
        }
        return stream.ToArray();
    }

    private sealed class StubHandler(byte[] archive, byte[] digest) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("github.com", request.RequestUri?.Host);
            byte[] body = request.RequestUri?.AbsolutePath.EndsWith(".dgst", StringComparison.OrdinalIgnoreCase) == true ? digest : archive;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
        }
    }
}
