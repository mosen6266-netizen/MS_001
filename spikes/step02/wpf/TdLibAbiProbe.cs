using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace MS001.Step02;

/// <summary>
/// Offline/native-interop-only probe. Does NOT supply Telegram API credentials or
/// authorize a user; it may return "native DLL missing" on most development PCs.
/// A single thread owns td_receive, as required by the modern TDLib JSON ABI.
/// </summary>
public static class TdLibAbiProbe
{
    [DllImport("tdjson", EntryPoint = "td_create_client_id", CallingConvention = CallingConvention.Cdecl)]
    private static extern int CreateClientId();

    [DllImport("tdjson", EntryPoint = "td_send", CallingConvention = CallingConvention.Cdecl,
        StringMarshalling = StringMarshalling.Utf8)]
    private static extern void Send(int clientId, string json);

    [DllImport("tdjson", EntryPoint = "td_receive", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr Receive(double timeoutSeconds);

    public static string ProbeVersion()
    {
        try
        {
            int clientId = CreateClientId();
            Send(clientId, "{\"@type\":\"getOption\",\"name\":\"version\",\"@extra\":\"step02-probe\"}");
            var elapsed = Stopwatch.StartNew();
            while (elapsed.Elapsed < TimeSpan.FromSeconds(5))
            {
                IntPtr ptr = Receive(0.25);
                if (ptr == IntPtr.Zero)
                    continue;
                // Copy before any further native call invalidates the TDLib pointer.
                string? response = Marshal.PtrToStringUTF8(ptr);
                if (string.IsNullOrWhiteSpace(response))
                    continue;
                using var doc = JsonDocument.Parse(response);
                if (doc.RootElement.TryGetProperty("@extra", out var extra)
                    && extra.GetString() == "step02-probe")
                {
                    return "tdjson loaded; version response: " + doc.RootElement.GetRawText();
                }
            }
            return "tdjson native library loaded, but version response timed out (NOT PASS).";
        }
        catch (DllNotFoundException) { return "tdjson.dll was not supplied (NOT RUN)."; }
        catch (EntryPointNotFoundException) { return "Required modern TDLib entrypoint is missing (FAIL)."; }
        catch (BadImageFormatException) { return "Native TDLib DLL bitness mismatch (FAIL)."; }
        catch (Exception e) { return "TDLib probe failed: " + e.GetType().Name; }
    }
}
