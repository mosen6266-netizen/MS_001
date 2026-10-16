using System.Globalization;
using Microsoft.Data.Sqlite;
using Mosen.GroupScheduler.Persistence;

namespace Mosen.GroupScheduler.History;

/// <summary>Fixed codes only. No arbitrary exception text, IP, token, Telegram message body,
/// user alias, account/chat identifier or external URL is accepted.</summary>
public enum HistoryCode {
    TaskReady,TaskRunning,TaskPaused,TaskReview,TaskCompleted,TaskStopped,TaskFailed,
    DeliveryPlanned,DeliverySubmitting,DeliveryPending,DeliveryConfirmed,DeliveryFailed,
    DeliveryUnknown,DeliveryReview,CorruptRecord,EmergencyStopped,HistoryPurged
}
public enum HistorySeverity { Info, Warning, Error }
public enum HistoryKind { Scheduler, Delivery, System }
public sealed record HistoryEntry(long Seq,DateTimeOffset EventUtc,HistoryKind Kind,
    HistoryCode Code,HistorySeverity Severity,Guid? ContextId);
public sealed record HistoryPage(IReadOnlyList<HistoryEntry> Items,long? NextBeforeSeq,bool HasMore);
public sealed record HistorySummary(long Total,long Warnings,long Errors,long Scheduler,long Delivery,long System);
public sealed record HistoryRetentionResult(int Removed,int RetentionDays,bool AuditWritten);
public sealed record SafeEvent(Guid SourceId,HistoryKind Kind,HistoryCode Code,
    HistorySeverity Severity,DateTimeOffset EventUtc,Guid? ContextId=null);

/// <summary>Independent, derived, non-authoritative history. Keyset pages use indexed seq,
/// never OFFSET over 100k rows. All writes are one-transaction and strictly whitelisted.</summary>
public sealed class HistoryRepository
{
    private readonly AppDatabase _database;
    public HistoryRepository(AppDatabase database)
    {
        _database=database??throw new ArgumentNullException(nameof(database));
        _database.Initialize();
    }
    private static string Stamp(DateTimeOffset value)=>value.ToUniversalTime()
        .ToString("O",CultureInfo.InvariantCulture);
    private SqliteConnection Open(bool readOnly=false)
    {
        var builder=new SqliteConnectionStringBuilder {
            DataSource=_database.FilePath,Mode=readOnly?SqliteOpenMode.ReadOnly:SqliteOpenMode.ReadWrite,
            Pooling=false,ForeignKeys=true,Cache=SqliteCacheMode.Private,DefaultTimeout=10
        };
        var connection=new SqliteConnection(builder.ToString());connection.Open();
        using var cmd=connection.CreateCommand();cmd.CommandText="PRAGMA busy_timeout=10000;";
        cmd.ExecuteNonQuery();return connection;
    }
    private static void CheckEvent(SafeEvent value)
    {
        if(value.SourceId==Guid.Empty||!Enum.IsDefined(value.Kind)||
           !Enum.IsDefined(value.Code)||!Enum.IsDefined(value.Severity)||
           value.ContextId==Guid.Empty||
           !MatchesKind(value.Code,value.Kind)||
           value.Severity!=SeverityFor(value.Code))
            throw new ArgumentException("Invalid safe history event contract.");
    }
    public static bool MatchesKind(HistoryCode code,HistoryKind kind)=>
        (code.ToString().StartsWith("Task",StringComparison.Ordinal)&&kind==HistoryKind.Scheduler)||
        (code.ToString().StartsWith("Delivery",StringComparison.Ordinal)&&kind==HistoryKind.Delivery)||
        ((code is HistoryCode.CorruptRecord or HistoryCode.EmergencyStopped or HistoryCode.HistoryPurged)
          &&kind==HistoryKind.System);
    public static HistorySeverity SeverityFor(HistoryCode code)=>code switch {
        HistoryCode.TaskFailed or HistoryCode.DeliveryUnknown or HistoryCode.DeliveryFailed or
            HistoryCode.CorruptRecord=>HistorySeverity.Error,
        HistoryCode.TaskReview or HistoryCode.TaskPaused or HistoryCode.DeliveryReview or
            HistoryCode.EmergencyStopped=>HistorySeverity.Warning,
        _=>HistorySeverity.Info
    };
    private static int Insert(SqliteConnection c,SqliteTransaction tx,string key,HistoryKind kind,
        HistoryCode code,HistorySeverity severity,DateTimeOffset at,Guid? context,
        DateTimeOffset recorded)
    {
        using var cmd=c.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText=@"INSERT OR IGNORE INTO history_events
          (source_key,event_utc,kind,code,severity,context_id,recorded_utc)
          VALUES($key,$at,$kind,$code,$severity,$context,$recorded);";
        cmd.Parameters.AddWithValue("$key",key);
        cmd.Parameters.AddWithValue("$at",Stamp(at));
        cmd.Parameters.AddWithValue("$kind",kind.ToString());
        cmd.Parameters.AddWithValue("$code",code.ToString());
        cmd.Parameters.AddWithValue("$severity",severity.ToString());
        cmd.Parameters.AddWithValue("$context",(object?)context?.ToString("D")??DBNull.Value);
        cmd.Parameters.AddWithValue("$recorded",Stamp(recorded));
        return cmd.ExecuteNonQuery();
    }
    /// <summary>Trusted host-only; idempotent by GUID. May batch at most 1000 per transaction.</summary>
    public int RecordSafeBatch(IReadOnlyList<SafeEvent> events,DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(events);
        if(events.Count>1000)throw new ArgumentOutOfRangeException(nameof(events));
        foreach(var entry in events)CheckEvent(entry);
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        int inserted=0;
        foreach(var e in events)
            inserted+=Insert(c,tx,"H:"+e.SourceId.ToString("D"),e.Kind,e.Code,e.Severity,
                e.EventUtc,e.ContextId,now);
        tx.Commit();return inserted;
    }

    /// <summary>Sync only safe status metadata from existing offline simulations and correlated
    /// delivery events. Repeated sync cannot manufacture duplicate timeline entries.</summary>
    public int CaptureLocalSnapshot(DateTimeOffset now)
    {
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        int inserted=0;
        using(var cmd=c.CreateCommand())
        {
            cmd.Transaction=tx;
            cmd.CommandText=@"SELECT s.run_id,s.state,s.revision,s.updated_utc
                FROM scheduler_simulation_jobs s
                WHERE NOT EXISTS(SELECT 1 FROM recovery_quarantine q
                  WHERE q.source='scheduler_simulation_jobs' AND q.record_id=s.run_id);";
            using var r=cmd.ExecuteReader();
            var safe=new List<(string id,string state,int revision,DateTimeOffset time)>();
            while(r.Read())
            {
                if(!Guid.TryParseExact(r.GetString(0),"D",out var id)||id==Guid.Empty||
                    !DateTimeOffset.TryParse(r.GetString(3),CultureInfo.InvariantCulture,
                        DateTimeStyles.None,out var when))continue;
                safe.Add((r.GetString(0),r.GetString(1),r.GetInt32(2),when));
            }
            foreach(var item in safe)
            {
                if(!Enum.TryParse<HistoryCode>("Task"+item.state,false,out var code)||
                   !MatchesKind(code,HistoryKind.Scheduler))continue;
                inserted+=Insert(c,tx,$"S:{item.id}:{item.revision}",HistoryKind.Scheduler,
                    code,SeverityFor(code),item.time,Guid.Parse(item.id),now);
            }
        }
        using(var cmd=c.CreateCommand())
        {
            cmd.Transaction=tx;
            cmd.CommandText=@"SELECT e.event_id,e.to_state,e.event_utc,a.run_id
                FROM delivery_events e JOIN delivery_attempts a ON a.attempt_id=e.attempt_id
                WHERE NOT EXISTS(SELECT 1 FROM recovery_quarantine q
                  WHERE q.source='delivery_attempts' AND q.record_id=a.attempt_id)
                ORDER BY e.event_utc DESC LIMIT 1000;";
            using var r=cmd.ExecuteReader();
            var safe=new List<(string id,string state,DateTimeOffset time,Guid? run)>();
            while(r.Read())
            {
                if(!Guid.TryParseExact(r.GetString(0),"D",out var ev)||ev==Guid.Empty||
                   !DateTimeOffset.TryParse(r.GetString(2),CultureInfo.InvariantCulture,
                       DateTimeStyles.None,out var at))continue;
                Guid? run=Guid.TryParseExact(r.GetString(3),"D",out var parsed)&&parsed!=Guid.Empty
                    ?parsed:null;
                safe.Add((r.GetString(0),r.GetString(1),at,run));
            }
            foreach(var item in safe)
            {
                if(!Enum.TryParse<HistoryCode>("Delivery"+item.state,false,out var code)||
                   !MatchesKind(code,HistoryKind.Delivery))continue;
                inserted+=Insert(c,tx,"D:"+item.id,HistoryKind.Delivery,code,
                    SeverityFor(code),item.time,item.run,now);
            }
        }
        tx.Commit();return inserted;
    }

    public HistoryPage ReadPage(int pageSize=10,long? beforeSeq=null,
        HistorySeverity? severity=null,Guid? contextId=null)
    {
        if(pageSize is not (10 or 20 or 50) ||
           beforeSeq is <=0 ||severity is not null&&!Enum.IsDefined(severity.Value)||
           contextId==Guid.Empty)throw new ArgumentException("Invalid bounded history query.");
        using var c=Open(true);using var tx=c.BeginTransaction(deferred:true);
        using var cmd=c.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText=@"SELECT seq,event_utc,kind,code,severity,context_id
            FROM history_events WHERE ($before IS NULL OR seq<$before)
            AND ($severity IS NULL OR severity=$severity)
            AND ($context IS NULL OR context_id=$context)
            ORDER BY seq DESC LIMIT $limit;";
        cmd.Parameters.AddWithValue("$before",(object?)beforeSeq??DBNull.Value);
        cmd.Parameters.AddWithValue("$severity",(object?)severity?.ToString()??DBNull.Value);
        cmd.Parameters.AddWithValue("$context",(object?)contextId?.ToString("D")??DBNull.Value);
        cmd.Parameters.AddWithValue("$limit",pageSize+1);
        var result=new List<HistoryEntry>(pageSize+1);
        using(var r=cmd.ExecuteReader())
        while(r.Read())
        {
            if(!DateTimeOffset.TryParse(r.GetString(1),CultureInfo.InvariantCulture,
                    DateTimeStyles.None,out var time)||
               !Enum.TryParse<HistoryKind>(r.GetString(2),false,out var kind)||
               !Enum.IsDefined(kind)||
               !Enum.TryParse<HistoryCode>(r.GetString(3),false,out var code)||
               !Enum.IsDefined(code)||
               !Enum.TryParse<HistorySeverity>(r.GetString(4),false,out var level)||
               !Enum.IsDefined(level))continue;
            Guid? context=r.IsDBNull(5)?null:Guid.Parse(r.GetString(5));
            result.Add(new(r.GetInt64(0),time,kind,code,level,context));
        }
        tx.Commit();
        bool more=result.Count>pageSize;
        if(more)result.RemoveAt(result.Count-1);
        return new(result,more&&result.Count>0?result[^1].Seq:null,more);
    }
    public HistorySummary Summarize()
    {
        using var c=Open(true);using var cmd=c.CreateCommand();
        cmd.CommandText=@"SELECT COUNT(*),
            coalesce(sum(CASE WHEN severity='Warning' THEN 1 ELSE 0 END),0),
            coalesce(sum(CASE WHEN severity='Error' THEN 1 ELSE 0 END),0),
            coalesce(sum(CASE WHEN kind='Scheduler' THEN 1 ELSE 0 END),0),
            coalesce(sum(CASE WHEN kind='Delivery' THEN 1 ELSE 0 END),0),
            coalesce(sum(CASE WHEN kind='System' THEN 1 ELSE 0 END),0)
            FROM history_events;";
        using var r=cmd.ExecuteReader();
        if(!r.Read())return new(0,0,0,0,0,0);
        return new(r.GetInt64(0),r.GetInt64(1),r.GetInt64(2),r.GetInt64(3),r.GetInt64(4),r.GetInt64(5));
    }
    /// <summary>User-initiated ONLY. Does not touch immutable delivery receipts, the real outbox,
    /// signed scripts, account sessions, group bindings or the emergency stop latch.</summary>
    public HistoryRetentionResult Prune(int retentionDays,DateTimeOffset now,bool confirmed)
    {
        if(!confirmed)throw new InvalidOperationException("Explicit user confirmation required.");
        if(retentionDays is not (7 or 30 or 90 or 180 or 365))
            throw new ArgumentOutOfRangeException(nameof(retentionDays));
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        using var cmd=c.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText="DELETE FROM history_events WHERE event_utc<$cutoff;";
        cmd.Parameters.AddWithValue("$cutoff",Stamp(now.AddDays(-retentionDays)));
        int removed=cmd.ExecuteNonQuery();
        if(removed>0)
            Insert(c,tx,"P:"+Guid.NewGuid().ToString("D"),HistoryKind.System,
                HistoryCode.HistoryPurged,HistorySeverity.Info,now,null,now);
        tx.Commit();return new(removed,retentionDays,removed>0);
    }
    /// <summary>Strong confirmation string is deliberately not an IPC operation.</summary>
    public HistoryRetentionResult ClearAll(string typedConfirmation,DateTimeOffset now)
    {
        if(typedConfirmation!="清理历史")throw new InvalidOperationException("Typed confirmation required.");
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        using var cmd=c.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText="DELETE FROM history_events;";
        int removed=cmd.ExecuteNonQuery();
        Insert(c,tx,"P:"+Guid.NewGuid().ToString("D"),HistoryKind.System,
            HistoryCode.HistoryPurged,HistorySeverity.Info,now,null,now);
        tx.Commit();return new(removed,0,true);
    }
}
