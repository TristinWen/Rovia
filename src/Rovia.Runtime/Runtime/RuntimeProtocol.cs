namespace Rovia.Runtime.Runtime;

/// <summary>Defines the compatibility contract shared by the Desktop and detached CLI runtime.</summary>
public static class RuntimeProtocol
{
    public const int Version = 3;
}

/// <summary>Describes the detached runtime binary used by a Desktop build.</summary>
public sealed record RuntimeProtocolInfo(int ProtocolVersion, string ProductVersion);
