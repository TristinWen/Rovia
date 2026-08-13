using Rovia.Config.Storage;
using Rovia.Core.Abstractions;
using Rovia.Core.Models;

namespace Rovia.Config.Tests;

/// <summary>Validates durable JSON node storage.</summary>
public sealed class JsonNodeRepositoryTests
{
    [Fact]
    public void Add_PersistsNodeAcrossInstances()
    {
        string path = Path.Combine(Path.GetTempPath(), $"rovia-{Guid.NewGuid():N}", "nodes.json");
        try
        {
            JsonNodeRepository repository = new(path);
            repository.Add(new ProxyNode { Id = "saved", Host = "example.com", Port = 443 });

            Assert.Equal("example.com", new JsonNodeRepository(path).Get("saved")?.Host);
        }
        finally
        {
            if (Directory.Exists(Path.GetDirectoryName(path)))
                Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    [Fact]
    public void Constructor_MigratesPlainCredentialsAndReturnsClearValues()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"rovia-{Guid.NewGuid():N}");
        string path      = Path.Combine(directory, "nodes.json");
        try
        {
            JsonNodeRepository clearRepository = new(path);
            clearRepository.Add(new ProxyNode
            {
                Id          = "secret",
                Host        = "example.com",
                Port        = 443,
                Credentials = new ProxyCredentials("uuid", "password")
            });
            JsonNodeRepository protectedRepository = new(path, new TestProtector());

            Assert.Equal("uuid", protectedRepository.Get("secret")?.Credentials?.Username);
            Assert.DoesNotContain("\"uuid\"", File.ReadAllText(path));
            Assert.Contains("test:uuid", File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private sealed class TestProtector : ICredentialProtector
    {
        public string Protect(string value) => IsProtected(value) ? value : $"test:{value}";
        public string Unprotect(string value) => IsProtected(value) ? value[5..] : value;
        public bool IsProtected(string value) => value.StartsWith("test:", StringComparison.Ordinal);
    }
}
