using Rovia.Core.Models;
using Rovia.Core.Routing;

namespace Rovia.Core.Tests;

public sealed class FallbackCandidateSelectorTests
{
    [Fact]
    public void ForManualSelection_ReturnsOnlyPrimaryWithoutExplicitGroup()
    {
        ProxyNode primary = Node("primary");

        IReadOnlyList<ProxyNode> result = FallbackCandidateSelector.ForManualSelection(primary, [primary, Node("other")]);

        Assert.Equal(["primary"], result.Select(node => node.Id));
    }

    [Fact]
    public void ForManualSelection_PutsPrimaryBeforeGroupedAlternatives()
    {
        ProxyNode primary = Node("primary", "office");
        ProxyNode grpc    = Node("grpc", "office");
        ProxyNode other   = Node("other", "home");

        IReadOnlyList<ProxyNode> result = FallbackCandidateSelector.ForManualSelection(primary, [grpc, other, primary]);

        Assert.Equal(["primary", "grpc"], result.Select(node => node.Id));
    }

    private static ProxyNode Node(string id, string? group = null) => new()
    {
        Id       = id,
        Host     = "example.com",
        Port     = 443,
        Metadata = group is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { [FallbackCandidateSelector.GroupMetadataKey] = group }
    };
}
