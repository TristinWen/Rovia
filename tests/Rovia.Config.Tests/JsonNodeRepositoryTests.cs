using Rovia.Config.Storage;
using Rovia.Core.Abstractions;
using Rovia.Core.Models;

namespace Rovia.Config.Tests;

/// <summary>Validates durable JSON node storage.</summary>
public sealed class JsonNodeRepositoryTests
{
    [Fact]
    public void GetAll_ReloadsChangesWrittenByAnotherProcessInstance()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string path      = Path.Combine(directory, "nodes.json");
        try
        {
            JsonNodeRepository host    = new(path);
            JsonNodeRepository desktop = new(path);

            desktop.Add(new() { Id = "external", Host = "example.com", Port = 443 });

            Assert.Equal("external", Assert.Single(host.GetAll()).Id);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task Add_SerializesConcurrentRepositoryWriters()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string path      = Path.Combine(directory, "nodes.json");
        try
        {
            JsonNodeRepository first  = new(path);
            JsonNodeRepository second = new(path);

            await Task.WhenAll(
                Task.Run(() => first.Add(new() { Id = "first", Host = "one.example", Port = 443 })),
                Task.Run(() => second.Add(new() { Id = "second", Host = "two.example", Port = 443 })));

            Assert.Equal(2, new JsonNodeRepository(path).GetAll().Count);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

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
