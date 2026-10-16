using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Mosen.GroupScheduler.Core.Security;

/// <summary>
/// Windows-only DPAPI CurrentUser envelope. This is a *local secret primitive*
/// for later account adapters, not a Telegram login/session implementation.
/// No secret bytes, exception messages or DPAPI encrypted tokens are logged.
/// </summary>
public static class DpapiProtector
{
    private const string Prefix = "MS001-DPAPI-USER-v1:";
    private const uint UiForbidden = 0x1;
    private static readonly byte[] Purpose = SHA256.HashData(
        Encoding.UTF8.GetBytes("MOSEN.GroupScheduler.LocalSecrets.v1"));

    [StructLayout(LayoutKind.Sequential)]
    private struct Blob
    {
        public int Length;
        public IntPtr Data;
    }

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref Blob input, string? description, ref Blob entropy, IntPtr reserved,
        IntPtr prompt, uint flags, out Blob output);

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref Blob input, out IntPtr description, ref Blob entropy, IntPtr reserved,
        IntPtr prompt, uint flags, out Blob output);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr pointer);

    public static string Protect(ReadOnlySpan<byte> secret)
    {
        RequireWindows();
        if (secret.Length is < 1 or > 65536) throw new ArgumentOutOfRangeException(nameof(secret));
        // A temporary copy allows pinning; callers should erase their own input buffer.
        byte[] plaintext = secret.ToArray();
        try
        {
            byte[] protectedBlob = Apply(plaintext, decrypt: false);
            try { return Prefix + Convert.ToBase64String(protectedBlob); }
            finally { CryptographicOperations.ZeroMemory(protectedBlob); }
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    /// <summary>Caller owns returned cleartext and must zero it after use.</summary>
    public static byte[] Unprotect(string envelope)
    {
        RequireWindows();
        if (envelope is null || !envelope.StartsWith(Prefix, StringComparison.Ordinal))
            throw new CryptographicException("Unknown secret envelope format.");
        // Reject unreasonably large inputs before base64 decoding.
        string encoded = envelope[Prefix.Length..];
        if (encoded.Length is < 4 or > 262144)
            throw new CryptographicException("Secret envelope length is invalid.");
        byte[] ciphertext;
        try { ciphertext = Convert.FromBase64String(encoded); }
        catch (FormatException) { throw new CryptographicException("Secret envelope encoding is invalid."); }
        try { return Apply(ciphertext, decrypt: true); }
        finally { CryptographicOperations.ZeroMemory(ciphertext); }
    }

    private static byte[] Apply(byte[] inputBytes, bool decrypt)
    {
        GCHandle pinInput = default, pinEntropy = default;
        Blob output = default;
        IntPtr description = IntPtr.Zero;
        try
        {
            pinInput = GCHandle.Alloc(inputBytes, GCHandleType.Pinned);
            pinEntropy = GCHandle.Alloc(Purpose, GCHandleType.Pinned);
            Blob input = new() { Length = inputBytes.Length, Data = pinInput.AddrOfPinnedObject() };
            Blob entropy = new() { Length = Purpose.Length, Data = pinEntropy.AddrOfPinnedObject() };
            bool ok = decrypt
                ? CryptUnprotectData(ref input, out description, ref entropy, IntPtr.Zero, IntPtr.Zero, UiForbidden, out output)
                : CryptProtectData(ref input, "MOSEN Local Credentials", ref entropy, IntPtr.Zero, IntPtr.Zero, UiForbidden, out output);
            if (!ok) throw new CryptographicException("Windows user data protection failed.",
                new Win32Exception(Marshal.GetLastWin32Error()));
            if (output.Length is < 1 or > 65536 || output.Data == IntPtr.Zero)
                throw new CryptographicException("Invalid DPAPI output.");
            byte[] result = new byte[output.Length];
            Marshal.Copy(output.Data, result, 0, output.Length);
            return result;
        }
        finally
        {
            if (output.Data != IntPtr.Zero) LocalFree(output.Data);
            if (description != IntPtr.Zero) LocalFree(description);
            if (pinInput.IsAllocated) pinInput.Free();
            if (pinEntropy.IsAllocated) pinEntropy.Free();
        }
    }

    private static void RequireWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows CurrentUser protection requires Windows.");
    }
}
