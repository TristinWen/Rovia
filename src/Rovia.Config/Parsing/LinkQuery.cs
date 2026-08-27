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
        if (type.Equals("tcp", StringComparison.OrdinalIgnoreCase))
            return null;
        ByteRange? padding = ParseByteRange(query.GetValueOrDefault("xPaddingFrom"), query.GetValueOrDefault("xPaddingTo"));
        ByteRange? post    = ParseByteRange(query.GetValueOrDefault("scPostFrom"), query.GetValueOrDefault("scPostTo"));
        return new(type,
            query.GetValueOrDefault("path"),
            query.GetValueOrDefault("host"),
            query.GetValueOrDefault("serviceName"),
            query.GetValueOrDefault("method"),
            query.GetValueOrDefault("idleTimeout"),
            query.GetValueOrDefault("pingTimeout"),
            null,
            query.GetValueOrDefault("mode"),
            padding,
            post);
    }

    private static ByteRange? ParseByteRange(string? from, string? to)
    {
        if (int.TryParse(from, out int f) && int.TryParse(to, out int t))
            return new ByteRange(f, t);
        return null;
    }
}
