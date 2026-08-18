using Rovia.Config.Storage;

namespace Rovia.Config.Subscriptions;

/// <summary>Refreshes persisted subscription providers and atomically replaces their nodes.</summary>
public sealed class SubscriptionRefreshService(
    JsonSubscriptionStore subscriptions,
    JsonNodeRepository nodes,
    SubscriptionImporter importer)
{
    public async Task<SubscriptionImportResult> RefreshAsync(SubscriptionDefinition definition, CancellationToken cancellationToken = default)
    {
        try
        {
            SubscriptionImportResult result = await importer.ImportAsync(definition.Source, cancellationToken);
            nodes.ReplaceSubscription(definition.Source.ToString(), result.Nodes);
            subscriptions.Upsert(definition with { LastRefreshedAt = DateTimeOffset.UtcNow, LastError = null });
            return result;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException)
        {
            subscriptions.Upsert(definition with { LastError = exception.Message });
            throw;
        }
    }

    public async Task<IReadOnlyList<SubscriptionImportResult>> RefreshDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        List<SubscriptionImportResult> results = [];
        foreach (SubscriptionDefinition definition in subscriptions.GetAll().Where(item => item.Enabled &&
                     (item.LastRefreshedAt is null || now - item.LastRefreshedAt >= item.RefreshInterval)))
        {
            try { results.Add(await RefreshAsync(definition, cancellationToken)); }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
                                               exception is (HttpRequestException or IOException or TaskCanceledException)) { }
        }
        return results;
    }
}
