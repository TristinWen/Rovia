using Rovia.Config.Storage;
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
}
