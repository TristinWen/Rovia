using System.Text;
using Rovia.Config.Parsing;
using Rovia.Config.Subscriptions;

namespace Rovia.Config.Tests;

/// <summary>Validates Base64 subscription parsing and stable node deduplication.</summary>
public sealed class SubscriptionImporterTests
{
    [Fact]
    public void Import_DecodesAndDeduplicatesEntries()
    {
        string link = "vless://11111111-1111-1111-1111-111111111111@example.com:443?security=tls#Node";
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{link}\n{link}"));
        SubscriptionImporter importer = new([new VlessLinkParser()]);

        SubscriptionImportResult result = importer.Import(encoded, "test");

        Assert.Single(result.Nodes);
        Assert.Equal(1, result.DuplicateCount);
        Assert.Equal("test", result.Nodes[0].Metadata["subscriptionSource"]);
    }
}
