using System.Net;
using Rovia.Core.Models;

namespace Rovia.Runtime.Diagnostics;

/// <summary>Builds temporary TLS transport candidates for each resolved edge address.</summary>
public static class ResolvedEndpointCandidateFactory
{
    public static IReadOnlyList<ProxyNode> Expand(ProxyNode node, IReadOnlyList<IPAddress> addresses)
    {
        if (IPAddress.TryParse(node.Host, out _) || node.Tls?.Enabled != true ||
            node.Transport?.Type.Equals("ws", StringComparison.OrdinalIgnoreCase) != true)
        {
            return [node];
        }

        IEnumerable<ProxyNode> resolved = addresses
            .Distinct()
            .Select(address => node with
            {
                Id        = $"{node.Id}@{address}",
                Name      = $"{DisplayName(node)} via {address}",
                Host      = address.ToString(),
                Tls       = node.Tls with { ServerName = node.Tls.ServerName ?? node.Host },
                Transport = node.Transport with { Host = node.Transport.Host ?? node.Host }
            });
        return new[] { node }.Concat(resolved).ToArray();
    }

    private static string DisplayName(ProxyNode node) => string.IsNullOrWhiteSpace(node.Name) ? node.Host : node.Name;
}
