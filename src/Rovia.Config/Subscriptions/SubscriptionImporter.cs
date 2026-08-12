using System.Security.Cryptography;
using System.Text;
using Rovia.Config.Parsing;
using Rovia.Core.Models;

namespace Rovia.Config.Subscriptions;

/// <summary>Downloads, decodes, parses, and deduplicates proxy subscription entries.</summary>
public sealed class SubscriptionImporter(IEnumerable<IProxyLinkParser> parsers, HttpClient? httpClient = null)
{
    private readonly IReadOnlyList<IProxyLinkParser> _parsers = [.. parsers];
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<SubscriptionImportResult> ImportAsync(Uri source, CancellationToken cancellationToken = default)
    {
        string content = await _httpClient.GetStringAsync(source, cancellationToken);
        return Import(content, source.ToString());
    }

    public SubscriptionImportResult Import(string content, string source = "manual")
    {
        string decoded = TryDecodeBase64(content.Trim());
        List<ProxyNode> nodes = [];
        List<string> warnings = [];
        HashSet<string> fingerprints = new(StringComparer.Ordinal);
        int duplicates = 0;
        foreach (string line in decoded.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            IProxyLinkParser? parser = _parsers.FirstOrDefault(candidate => candidate.CanParse(line));
            if (parser is null)
            {
                warnings.Add("Skipped an unsupported subscription entry.");
                continue;
            }
            try
            {
                ProxyNode parsed = parser.Parse(line);
                string fingerprint = Fingerprint(parsed);
                if (!fingerprints.Add(fingerprint))
                {
                    duplicates++;
                    continue;
                }
                Dictionary<string, string> metadata = new(parsed.Metadata, StringComparer.OrdinalIgnoreCase)
                {
                    ["subscriptionSource"] = source,
                    ["fingerprint"]        = fingerprint
                };
                nodes.Add(parsed with { Id = fingerprint[..24], Metadata = metadata });
            }
            catch (ProxyLinkParseException exception)
            {
                warnings.Add(exception.Message);
            }
        }
        return new(nodes, duplicates, warnings);
    }

    private static string TryDecodeBase64(string value)
    {
        if (value.Contains("://", StringComparison.Ordinal))
            return value;
        try
        {
            string normalized = value.Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight((normalized.Length + 3) / 4 * 4, '=');
            return Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
        }
        catch (FormatException)
        {
            return value;
        }
    }

    private static string Fingerprint(ProxyNode node)
    {
        string canonical = $"{node.Protocol}|{node.Host.ToLowerInvariant()}|{node.Port}|{node.Credentials.Username}|{node.Transport?.Type}|{node.Transport?.Path}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
