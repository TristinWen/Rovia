using System.Text;
using System.Text.Json;
using Rovia.Core.Models;

namespace Rovia.Config.Parsing;

/// <summary>Parses common Base64-JSON VMess share links into backend-independent nodes.</summary>
public sealed class VmessLinkParser : IProxyLinkParser
{
    public bool CanParse(string input) => input.StartsWith("vmess://", StringComparison.OrdinalIgnoreCase);

    public ProxyNode Parse(string input)
    {
        try
        {
            string encoded = input[8..].Replace('-', '+').Replace('_', '/');
            encoded = encoded.PadRight((encoded.Length + 3) / 4 * 4, '=');
            using JsonDocument document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));
            JsonElement root = document.RootElement;
            string id = Required(root, "id");
            if (!Guid.TryParse(id, out _))
                throw new FormatException();
            string host = Required(root, "add");
            if (!int.TryParse(Required(root, "port"), out int port))
                throw new FormatException();
            string security = Get(root, "tls") ?? string.Empty;
            string type = Get(root, "net") ?? "tcp";
            return new()
            {
                Name = Get(root, "ps") ?? string.Empty, Protocol = ProxyProtocol.Vmess, Host = host, Port = port,
                Credentials = new(id), Tls = string.IsNullOrWhiteSpace(security) ? null : new(true, Get(root, "sni"), Get(root, "fp")),
                Transport = type == "tcp" ? null : new(type, Get(root, "path"), Get(root, "host"), Get(root, "path")),
                Metadata = new Dictionary<string, string> { ["alterId"] = Get(root, "aid") ?? "0", ["security"] = Get(root, "scy") ?? "auto" }
            };
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new ProxyLinkParseException("The value is not a valid VMess share link.");
        }
    }

    private static string Required(JsonElement element, string name) => Get(element, name) ?? throw new FormatException();
    private static string? Get(JsonElement element, string name) => element.TryGetProperty(name, out JsonElement value) ? value.ToString() : null;
}
