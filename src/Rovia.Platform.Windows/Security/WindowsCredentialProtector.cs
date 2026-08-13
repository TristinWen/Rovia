using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Rovia.Core.Abstractions;

namespace Rovia.Platform.Windows.Security;

/// <summary>Protects credentials with Windows DPAPI for the current user profile.</summary>
public sealed class WindowsCredentialProtector : ICredentialProtector
{
    private const string Prefix = "dpapi:v1:";

    public string Protect(string value)
    {
        if (string.IsNullOrEmpty(value) || IsProtected(value))
            return value;
        byte[] clearBytes = Encoding.UTF8.GetBytes(value);
        return Prefix + Convert.ToBase64String(Transform(clearBytes, protect: true));
    }

    public string Unprotect(string value)
    {
        if (string.IsNullOrEmpty(value) || !IsProtected(value))
            return value;
        byte[] protectedBytes = Convert.FromBase64String(value[Prefix.Length..]);
        return Encoding.UTF8.GetString(Transform(protectedBytes, protect: false));
    }

    public bool IsProtected(string value) => value.StartsWith(Prefix, StringComparison.Ordinal);

    private static byte[] Transform(byte[] input, bool protect)
    {
        DataBlob inputBlob  = new(input);
        DataBlob outputBlob = default;
        try
        {
            bool succeeded = protect
                ? CryptProtectData(ref inputBlob, null, nint.Zero, nint.Zero, nint.Zero, 0, out outputBlob)
                : CryptUnprotectData(ref inputBlob, nint.Zero, nint.Zero, nint.Zero, nint.Zero, 0, out outputBlob);
            if (!succeeded)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            byte[] output = new byte[outputBlob.Length];
            Marshal.Copy(outputBlob.Data, output, 0, output.Length);
            return output;
        }
        finally
        {
            inputBlob.Dispose();
            if (outputBlob.Data != nint.Zero)
                LocalFree(outputBlob.Data);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob : IDisposable
    {
        public int Length;
        public nint Data;

        public DataBlob(byte[] bytes)
        {
            Length = bytes.Length;
            Data   = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, Data, bytes.Length);
        }

        public void Dispose()
        {
            if (Data == nint.Zero)
                return;
            Marshal.FreeHGlobal(Data);
            Data = nint.Zero;
        }
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(ref DataBlob input, string? description, nint entropy, nint reserved, nint prompt, int flags, out DataBlob output);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(ref DataBlob input, nint description, nint entropy, nint reserved, nint prompt, int flags, out DataBlob output);

    [DllImport("kernel32.dll")]
    private static extern nint LocalFree(nint memory);
}
