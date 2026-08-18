using Rovia.Config.Storage;
using Rovia.Core.Policies;

namespace Rovia.Config.Tests;

public sealed class JsonRoutingConfigurationStoreTests
{
    [Fact]
    public void Write_ReadsRulesAndDnsPolicy()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string path      = Path.Combine(directory, "routing.json");
        try
        {
            JsonRoutingConfigurationStore store = new(path);
            store.Write(new()
            {
                Rules = [new() { DomainSuffixes = ["internal.example"], Action = RouteAction.Direct }],
                Dns   = new() { PreferIpv6 = true, EnableCache = false }
            });

            RoutingConfiguration loaded = store.Read();

            Assert.Equal(RouteAction.Direct, Assert.Single(loaded.Rules).Action);
            Assert.True(loaded.Dns.PreferIpv6);
            Assert.False(loaded.Dns.EnableCache);
            Assert.Contains("\"Direct\"", File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
