using System.Net;
using Rovia.Config.Parsing;
using Rovia.Config.Storage;
using Rovia.Config.Subscriptions;

namespace Rovia.Config.Tests;

public sealed class SubscriptionRefreshServiceTests
{
    [Fact]
    public async Task RefreshAsync_ReplacesOnlyProviderNodesAndPreservesLabels()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            JsonNodeRepository nodes          = new(Path.Combine(directory, "nodes.json"));
            JsonSubscriptionStore definitions = new(Path.Combine(directory, "subscriptions.json"));
            Uri source                        = new("https://example.com/provider");
            SubscriptionDefinition definition = new() { Id = "provider", Name = "Primary", Source = source };
            definitions.Upsert(definition);
            SubscriptionImporter firstImporter = CreateImporter("vless://11111111-1111-1111-1111-111111111111@example.com:443?security=tls#Original");
            await new SubscriptionRefreshService(definitions, nodes, firstImporter).RefreshAsync(definition);
            string nodeId = Assert.Single(nodes.GetAll()).Id;
            nodes.Add(nodes.Get(nodeId)! with { Name = "Pinned label" });

            SubscriptionImporter secondImporter = CreateImporter("vless://11111111-1111-1111-1111-111111111111@example.com:443?security=tls#Provider label\n" +
                                                                  "vless://22222222-2222-2222-2222-222222222222@example.net:443?security=tls#Second");
            await new SubscriptionRefreshService(definitions, nodes, secondImporter).RefreshAsync(definition);

            Assert.Equal(2, nodes.GetAll().Count);
            Assert.Equal("Pinned label", nodes.Get(nodeId)!.Name);
            Assert.NotNull(Assert.Single(definitions.GetAll()).LastRefreshedAt);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task RefreshDueAsync_SkipsProvidersInsideTheirInterval()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            JsonSubscriptionStore definitions = new(Path.Combine(directory, "subscriptions.json"));
            definitions.Upsert(new()
            {
                Id = "fresh", Name = "Fresh", Source = new("https://example.com/fresh"),
                LastRefreshedAt = DateTimeOffset.UtcNow, RefreshInterval = TimeSpan.FromHours(1)
            });
            SubscriptionRefreshService service = new(definitions, new(Path.Combine(directory, "nodes.json")),
                CreateImporter("vless://11111111-1111-1111-1111-111111111111@example.com:443?security=tls#First"));

            Assert.Empty(await service.RefreshDueAsync(DateTimeOffset.UtcNow));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task RefreshDueAsync_ContinuesAfterProviderFailure()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            JsonSubscriptionStore definitions = new(Path.Combine(directory, "subscriptions.json"));
            definitions.Upsert(new() { Id = "broken", Name = "Broken", Source = new("https://example.com/broken") });
            definitions.Upsert(new() { Id = "healthy", Name = "Healthy", Source = new("https://example.com/healthy") });
            HttpClient client = new(new RoutingHandler(request => request.RequestUri!.AbsolutePath == "/broken"
                ? Task.FromException<HttpResponseMessage>(new HttpRequestException("offline"))
                : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("vless://11111111-1111-1111-1111-111111111111@example.com:443?security=tls#First")
                })));
            SubscriptionRefreshService service = new(definitions, new(Path.Combine(directory, "nodes.json")),
                new([new VlessLinkParser()], client));

            Assert.Single(await service.RefreshDueAsync(DateTimeOffset.UtcNow));
            Assert.Equal("offline", definitions.GetAll().Single(item => item.Id == "broken").LastError);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private static SubscriptionImporter CreateImporter(string content) => new([new VlessLinkParser()],
        new HttpClient(new StubHandler(content)));

    private sealed class StubHandler(string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) });
    }

    private sealed class RoutingHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => response(request);
    }
}
