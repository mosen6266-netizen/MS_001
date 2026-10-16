using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Mosen.GroupScheduler.History;
public sealed record SupportBundleResult(byte[] Archive,string TicketId,int IncludedRows);
/// <summary>Freshly generates data from fixed enums and coarse counters. NEVER adds a raw log,
/// SQLite DB, account, chat/topic, run ID, phone, message, stack trace, session or absolute path.</summary>
public static class SupportBundleBuilder
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    public static SupportBundleResult Build(HistoryRepository repository,DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var summary=repository.Summarize();
        var rows=new List<HistoryEntry>(200);
        long? cursor=null;
        for(int i=0;i<4;i++)
        {
            var page=repository.ReadPage(50,cursor);
            rows.AddRange(page.Items);
            if(!page.HasMore)break;
            cursor=page.NextBeforeSeq;
        }
        var ticket=Guid.NewGuid().ToString("N");
        using var buffer=new MemoryStream();
        using(var zip=new ZipArchive(buffer,ZipArchiveMode.Create,true,Encoding.UTF8))
        {
            AddJson(zip,"manifest.json",new{
                format="MOSEN-SUPPORT-REPORT-1",ticketId=ticket,
                generatedUtc=now.ToUniversalTime().ToString("O"),
                dataMode="offline-derived-history",rowsIncluded=rows.Count,
                disclosure="Only fixed operational categories and coarse event counts; no Telegram content."
            });
            AddJson(zip,"summary.json",new{
                total=summary.Total,warnings=summary.Warnings,errors=summary.Errors,
                scheduler=summary.Scheduler,delivery=summary.Delivery,system=summary.System
            });
            // Do not include source key, internal GUID, run ID or sequence: fixed enum only.
            var timeline=zip.CreateEntry("timeline.jsonl",CompressionLevel.Optimal);
            using var writer=new StreamWriter(timeline.Open(),new UTF8Encoding(false));
            foreach(var row in rows)
                writer.WriteLine(JsonSerializer.Serialize(new {
                    utc=row.EventUtc.ToUniversalTime().ToString("O"),
                    kind=row.Kind.ToString(),code=row.Code.ToString(),
                    severity=row.Severity.ToString()
                },Json));
        }
        if(buffer.Length>256*1024)throw new InvalidOperationException("Sanitized package exceeds size ceiling.");
        return new(buffer.ToArray(),ticket,rows.Count);
    }
    private static void AddJson(ZipArchive zip,string name,object data)
    {
        using var stream=zip.CreateEntry(name,CompressionLevel.Optimal).Open();
        JsonSerializer.Serialize(stream,data,Json);
    }
}

/// <summary>Fixed-owned filename maintenance. User must explicitly confirm; never recursively
/// clear profile files and never follow reparse/symlink paths.</summary>
public sealed class SafeDiagnosticLogRetention
{
    private static readonly string[] Owned={"events.jsonl","events.previous.jsonl"};
    private readonly string _directory;
    public SafeDiagnosticLogRetention(string directory)
    {
        if(string.IsNullOrWhiteSpace(directory))throw new ArgumentException("Missing safe log directory.");
        _directory=Path.GetFullPath(directory);
    }
    public IReadOnlyList<string> Preview(DateTimeOffset cutoffUtc)
    {
        if(!Directory.Exists(_directory))return Array.Empty<string>();
        var found=new List<string>();
        foreach(var name in Owned)
        {
            var path=Path.Combine(_directory,name);
            if(!File.Exists(path))continue;
            var meta=new FileInfo(path);
            if((meta.Attributes&FileAttributes.ReparsePoint)!=0)continue;
            if(meta.LastWriteTimeUtc<cutoffUtc.UtcDateTime)found.Add(name);
        }
        return found;
    }
    public int Prune(int retentionDays,DateTimeOffset now,bool confirmed)
    {
        if(!confirmed)throw new InvalidOperationException("Explicit confirmation required.");
        if(retentionDays is not (7 or 30 or 90 or 180 or 365))
            throw new ArgumentOutOfRangeException(nameof(retentionDays));
        int count=0;
        foreach(var name in Preview(now.AddDays(-retentionDays)))
        {
            File.Delete(Path.Combine(_directory,name));
            count++;
        }
        return count;
    }
}
