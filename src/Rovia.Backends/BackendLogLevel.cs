namespace Rovia.Backends;

/// <summary>Infers backend log severity when a core writes all messages to one stream.</summary>
public static class BackendLogLevel
{
    public static string Classify(string fallback, string message)
    {
        if (message.Contains(" FATAL ", StringComparison.OrdinalIgnoreCase) || message.Contains("[FATAL]", StringComparison.OrdinalIgnoreCase))
            return "ERROR";
        if (message.Contains(" ERROR ", StringComparison.OrdinalIgnoreCase) || message.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase))
            return "ERROR";
        if (message.Contains(" WARN ", StringComparison.OrdinalIgnoreCase) || message.Contains("[WARNING]", StringComparison.OrdinalIgnoreCase))
            return "WARN";
        if (message.Contains(" INFO ", StringComparison.OrdinalIgnoreCase) || message.Contains("[INFO]", StringComparison.OrdinalIgnoreCase))
            return "INFO";
        if (message.Contains(" DEBUG ", StringComparison.OrdinalIgnoreCase) || message.Contains("[DEBUG]", StringComparison.OrdinalIgnoreCase))
            return "DEBUG";
        return fallback;
    }
}
