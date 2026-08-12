using Rovia.Core.Models;
using Rovia.Core.Repositories;

namespace Rovia.Core.Tests;

/// <summary>Validates the in-memory node repository.</summary>
public sealed class MemoryNodeRepositoryTests
{
    [Fact]
    public void Add_ReplacesNodeWithSameId()
    {
        MemoryNodeRepository repository = new();
        ProxyNode first                 = new() { Id = "node", Name = "First", Host = "localhost", Port = 443 };
        ProxyNode replacement           = first with { Name = "Replacement" };

        repository.Add(first);
        repository.Add(replacement);

        Assert.Equal("Replacement", repository.Get("node")?.Name);
        Assert.Single(repository.GetAll());
    }
}
