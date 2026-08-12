using System.Net;
using Rovia.Core.Models;

namespace Rovia.Config.Parsing;

/// <summary>Shares query and transport mapping across URI-based link parsers.</summary>
internal static class LinkQuery
{
    public static Dictionary<string, string> Parse(string query) => query.TrimStart('?')
        .Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(item => item.Split('=', 2))
        .ToDictionary(pair => WebUtility.UrlDecode(pair[0]), pair => pair.Length == 2 ? WebUtility.UrlDecode(pair[1]) : string.Empty, StringComparer.OrdinalIgnoreCase);

    public static TransportOptions? Transport(IReadOnlyDictionary<string, string> query)
    {
        string type = query.GetValueOrDefault("type") ?? "tcp";
        return type.Equals("tcp", StringComparison.OrdinalIgnoreCase) ? null
            : new(type, query.GetValueOrDefault("path"), query.GetValueOrDefault("host"), query.GetValueOrDefault("serviceName"));
    }
}
