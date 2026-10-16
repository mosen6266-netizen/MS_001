using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Mosen.GroupScheduler.Core.Telemetry;

/// <summary>
/// Telemetry intentionally has NO arbitrary string payload or exception APIs.
/// Codes and coarse counters only: never pass exception.Message, stack, URLs,
/// account identities, chat names, phone numbers, auth tokens or raw IPC requests.
/// </summary>
public enum DiagnosticEvent { Started, Ready, Closed, SettingsRead, SettingsSaved, SettingsConflict, IpcRejected, RecoverableError, FatalError }
public enum FailureCategory { None, FileSystem, Database, Protocol, NativeLibrary, Unknown }
public sealed record HealthSnapshot(
    int SchemaVersion, string AppState, long UptimeSeconds, long Events,
    long Warnings, long Failures, long WorkingSetMiB, long CurrentThreads,
    long OpenHandles, bool WindowsHandlesAvailable);

public sealed class SafeDiagnostics
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly object _gate = new();
    private readonly Stopwatch _uptime = Stopwatch.StartNew();
    private readonly string _directory;
    private readonly string _log;
    private readonly string _archive;
    private long _events, _warnings, _failures;
    private readonly long _limitBytes;

    public SafeDiagnostics(string directory, long maxBytes = 256 * 1024)
    {
        if (maxBytes is < 1024 or > 256 * 1024) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        _limitBytes = maxBytes;
        _directory = Path.GetFullPath(directory ?? throw new ArgumentNullException(nameof(directory)));
        _log = Path.Combine(_directory,"events.jsonl");
        _archive = Path.Combine(_directory,"events.previous.jsonl");
    }
    public static SafeDiagnostics ForCurrentUser()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("LocalAppData unavailable.");
        return new SafeDiagnostics(Path.Combine(root,"MOSEN","TelegramScheduler","diagnostics"));
    }

    /// <returns>True if the event was durably appended. False on readonly/full/locked disk.</returns>
    public bool Record(DiagnosticEvent code, FailureCategory failure=FailureCategory.None)
    {
        if (!Enum.IsDefined(code) || !Enum.IsDefined(failure)) throw new ArgumentOutOfRangeException(nameof(code));
        if (code is DiagnosticEvent.RecoverableError or DiagnosticEvent.FatalError && failure == FailureCategory.None)
            throw new ArgumentException("Error event must use a known failure category.");
        if (code is not (DiagnosticEvent.RecoverableError or DiagnosticEvent.FatalError) && failure != FailureCategory.None)
            throw new ArgumentException("Non-error event cannot include a failure category.");
        lock (_gate)
        {
            _events++;
            if (code is DiagnosticEvent.SettingsConflict or DiagnosticEvent.IpcRejected or DiagnosticEvent.RecoverableError) _warnings++;
            if (code == DiagnosticEvent.FatalError) _failures++;
            try
            {
                Directory.CreateDirectory(_directory);
                string line = JsonSerializer.Serialize(new {
                    version=1,
                    utc=DateTimeOffset.UtcNow.ToString("O"),
                    code=code.ToString(),
                    category=failure.ToString()
                },Json)+"\n";
                if (File.Exists(_log) && new FileInfo(_log).Length+Encoding.UTF8.GetByteCount(line)>_limitBytes)
                    File.Move(_log,_archive,overwrite:true);
                // One append is bounded and flushed; never logs content from callers.
                using var file = new FileStream(_log,FileMode.Append,FileAccess.Write,FileShare.Read,4096,FileOptions.WriteThrough);
                byte[] bytes=Encoding.UTF8.GetBytes(line);
                file.Write(bytes);
                file.Flush(flushToDisk:true);
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    public HealthSnapshot Snapshot(string appState)
    {
        if (appState is not ("initializing" or "ready" or "degraded" or "closing"))
            throw new ArgumentException("Invalid public app state.");
        long memory = 0, threads=0, handles=0;
        bool windowsHandles=false;
        try
        {
            using var process=Process.GetCurrentProcess();
            memory=Math.Max(0,process.WorkingSet64/(1024*1024));
            threads=process.Threads.Count;
            if (OperatingSystem.IsWindows()){handles=process.HandleCount;windowsHandles=true;}
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Process counters unavailable, no raw error details surfaced.
        }
        lock (_gate)
        {
            return new HealthSnapshot(1,appState,Math.Max(0,(long)_uptime.Elapsed.TotalSeconds),
                _events,_warnings,_failures,memory,threads,handles,windowsHandles);
        }
    }
}

public static class FailureClassifier
{
    public static FailureCategory Classify(Exception error) => error switch
    {
        IOException or UnauthorizedAccessException => FailureCategory.FileSystem,
        System.Data.Common.DbException => FailureCategory.Database,
        System.Text.Json.JsonException or ArgumentException => FailureCategory.Protocol,
        DllNotFoundException or BadImageFormatException or EntryPointNotFoundException => FailureCategory.NativeLibrary,
        _ => FailureCategory.Unknown
    };
}
