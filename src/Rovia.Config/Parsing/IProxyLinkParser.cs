using Rovia.Core.Models;

namespace Rovia.Config.Parsing;

/// <summary>Converts a supported proxy share link into a unified node.</summary>
public interface IProxyLinkParser
{
    bool CanParse(string input);
    ProxyNode Parse(string input);
}
