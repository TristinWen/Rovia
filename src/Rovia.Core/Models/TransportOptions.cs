namespace Rovia.Core.Models;

/// <summary>Random byte range used by XHTTP padding and post-size limits.</summary>
public sealed record ByteRange(int From, int To);

/// <summary>Controls XHTTP HTTP/2 and HTTP/3 connection reuse.</summary>
public sealed record XhttpXmuxOptions(
    int? MaxConcurrency = null,
    int? MaxConnections = null,
    int? CMaxReuseTimes = null,
    int? HMaxRequestTimes = null,
    int? HMaxReusableSecs = null,
    int? HKeepAlivePeriod = null);

/// <summary>Describes protocol transport options independent of a backend schema.</summary>
public sealed record TransportOptions(
    string Type,
    string? Path = null,
    string? Host = null,
    string? ServiceName = null,
    string? Method = null,
    string? IdleTimeout = null,
    string? PingTimeout = null,
    IDictionary<string, string>? Headers = null,
    string? XhttpMode = null,
    ByteRange? XPaddingBytes = null,
    ByteRange? SCMaxEachPostBytes = null,
    XhttpXmuxOptions? XhttpXmux = null);
