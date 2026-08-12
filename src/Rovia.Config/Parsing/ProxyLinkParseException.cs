namespace Rovia.Config.Parsing;

/// <summary>Reports a user-correctable proxy share-link error.</summary>
public sealed class ProxyLinkParseException(string message) : FormatException(message);
