namespace Rovia.Config.Subscriptions;

/// <summary>Describes a persisted proxy subscription provider.</summary>
public sealed record SubscriptionDefinition
{
    public string          Id              { get; init; } = Guid.NewGuid().ToString("N");
    public string          Name            { get; init; } = string.Empty;
    public Uri             Source          { get; init; } = null!;
    public bool            Enabled         { get; init; } = true;
    public TimeSpan        RefreshInterval { get; init; } = TimeSpan.FromHours(6);
    public DateTimeOffset? LastRefreshedAt { get; init; }
    public string?         LastError       { get; init; }
}
