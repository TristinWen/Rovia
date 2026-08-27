using System.Net;
using System.Net.Sockets;

namespace Rovia.Runtime.Runtime;

/// <summary>Validates local runtime prerequisites before starting a proxy backend.</summary>
public static class RuntimePreflight
{
    public static void EnsurePortAvailable(int port)
    {
        if (port is < 1 or > 65535)
            throw new InvalidOperationException($"Proxy listen port {port} is outside the valid range.");
        TcpListener listener = new(IPAddress.Loopback, port);
        try { listener.Start(); }
        catch (SocketException exception)
        {
            throw new InvalidOperationException($"Proxy listen port {port} is already in use. Close the conflicting application or select another port.", exception);
        }
        finally { listener.Stop(); }
    }

    public static bool StopOrphanedBackend(RuntimeState? state)
    {
        if (state is null || state.IsRunning || state.BackendProcessId is not int processId)
            return false;
        try
        {
            using System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessById(processId);
            bool recognized = process.ProcessName.Equals("sing-box", StringComparison.OrdinalIgnoreCase)
                              || process.ProcessName.Equals("xray", StringComparison.OrdinalIgnoreCase);
            if (!recognized)
                return false;
            process.Kill(true);
            process.WaitForExit(5000);
            return true;
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }
}
