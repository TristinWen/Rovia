using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Rovia.Backends.SingBox;

namespace Rovia.Backends.Tests;

/// <summary>Validates verified sing-box installation from an official release response.</summary>
public sealed class SingBoxProvisionerTests
{
    [Fact]
    public async Task EnsureAsync_DownloadsVerifiesAndExtractsMissingBinary()
    {
        byte[] archive = CreateArchive();
        string digest  = Convert.ToHexString(SHA256.HashData(archive));
        string json    = $$"""{"tag_name":"v1.2.3","assets":[{"name":"sing-box-1.2.3-windows-amd64.zip","browser_download_url":"https://example.test/sing-box.zip","digest":"sha256:{{digest}}"}]}""";
        HttpClient client = new(new StubHandler(json, archive));
        string directory  = Path.Combine(Path.GetTempPath(), $"rovia-sing-box-{Guid.NewGuid():N}");
        try
        {
            string path = await new SingBoxProvisioner(client).EnsureAsync(directory);

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
            ZipArchiveEntry entry = zip.CreateEntry("sing-box-1.2.3-windows-amd64/sing-box.exe");
            using StreamWriter writer = new(entry.Open());
            writer.Write("binary");
        }
        return stream.ToArray();
    }

    private sealed class StubHandler(string releaseJson, byte[] archive) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = request.RequestUri?.Host == "api.github.com"
                ? new(HttpStatusCode.OK) { Content = new StringContent(releaseJson, Encoding.UTF8, "application/json") }
                : new(HttpStatusCode.OK) { Content = new ByteArrayContent(archive) };
            return Task.FromResult(response);
        }
    }
}
