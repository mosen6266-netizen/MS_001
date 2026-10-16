using System.Runtime.InteropServices;

namespace Mosen.GroupScheduler.Telegram;

/// <summary>
/// Narrow ABI boundary for official TDLib JSON C interface.
/// Receive is process-global: one coordinator must own all clients.
/// Test doubles do not load the native DLL.
/// </summary>
public interface ITdlibTransport : IDisposable
{
    int CreateClientId();
    void Send(int clientId, string json);
    string? Receive(TimeSpan timeout);
}

public sealed class NativeTdlibTransport : ITdlibTransport
{
    private static int _nativeOwner;
    private bool _disposed;

    public NativeTdlibTransport()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The packaged TDLib native target is Windows x64.");
        if (IntPtr.Size != 8)
            throw new PlatformNotSupportedException("TDLib requires win-x64.");
        if (Interlocked.CompareExchange(ref _nativeOwner, 1, 0) != 0)
            throw new InvalidOperationException("Only one TDLib JSON receiver may exist in a process.");
    }

    [DllImport("tdjson", EntryPoint="td_create_client_id", CallingConvention=CallingConvention.Cdecl)]
    private static extern int NativeCreateClientId();

    [DllImport("tdjson", EntryPoint="td_send", CallingConvention=CallingConvention.Cdecl)]
    private static extern void NativeSend(int clientId, [MarshalAs(UnmanagedType.LPUTF8Str)] string json);

    [DllImport("tdjson", EntryPoint="td_receive", CallingConvention=CallingConvention.Cdecl)]
    private static extern IntPtr NativeReceive(double timeoutSeconds);

    public int CreateClientId()
    {
        ThrowIfDisposed();
        return NativeCreateClientId();
    }
    public void Send(int clientId, string json)
    {
        ThrowIfDisposed();
        if (clientId <= 0) throw new ArgumentOutOfRangeException(nameof(clientId));
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        NativeSend(clientId, json);
    }
    public string? Receive(TimeSpan timeout)
    {
        ThrowIfDisposed();
        if (timeout < TimeSpan.Zero || timeout > TimeSpan.FromSeconds(1))
            throw new ArgumentOutOfRangeException(nameof(timeout));
        IntPtr pointer = NativeReceive(timeout.TotalSeconds);
        if (pointer == IntPtr.Zero) return null;
        // Copy returned memory immediately; native pointer lifetime ends at next td_receive.
        string message = Marshal.PtrToStringUTF8(pointer) ?? throw new InvalidDataException("Invalid TDLib UTF-8 response.");
        if (message.Length > 1024 * 1024)
            throw new InvalidDataException("Oversize TDLib response; account transport must be reviewed.");
        return message;
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Interlocked.Exchange(ref _nativeOwner, 0);
        // Modern TDLib destroys closed clients automatically. This transport does NOT unload
        // the process-global library. Caller must close clients before leaving the process.
    }
    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(NativeTdlibTransport));
    }
}
