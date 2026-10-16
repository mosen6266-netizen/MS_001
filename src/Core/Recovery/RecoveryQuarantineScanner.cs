using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Mosen.GroupScheduler.Delivery;
using Mosen.GroupScheduler.Persistence;
using Mosen.GroupScheduler.StateMachine;
using Mosen.GroupScheduler.Scheduler;

namespace Mosen.GroupScheduler.Recovery;

public sealed record QuarantinedRecord(string Source,string RecordId,string Reason,DateTimeOffset DiscoveredUtc);
/// <summary>Storage is NEVER repaired by deleting rows: damaged rows remain available to inspect
/// but become ineligible for all normal scheduling/delivery enumerations.</summary>
public sealed class RecoveryQuarantineScanner
{
    private readonly AppDatabase _db;
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    public RecoveryQuarantineScanner(AppDatabase database)=>
        _db=database??throw new ArgumentNullException(nameof(database));
    private SqliteConnection Open()
    {
        _db.Initialize();
        var c=new SqliteConnection(new SqliteConnectionStringBuilder{
            DataSource=_db.FilePath,Mode=SqliteOpenMode.ReadWrite,ForeignKeys=true,
            Pooling=false,Cache=SqliteCacheMode.Private,DefaultTimeout=10}.ToString());
        c.Open();
        using var cmd=c.CreateCommand();cmd.CommandText="PRAGMA foreign_keys=ON;PRAGMA busy_timeout=10000;";
        cmd.ExecuteNonQuery();return c;
    }
    private static bool ValidGuid(string? s)=>
        Guid.TryParseExact(s,"D",out var id)&&id!=Guid.Empty;
    private static bool ValidChat(string? s)=>
        long.TryParse(s,NumberStyles.Integer,CultureInfo.InvariantCulture,out var id)&&id!=0;
    private static bool ValidTopic(string? s)=>s is null||
        long.TryParse(s,NumberStyles.Integer,CultureInfo.InvariantCulture,out var id)&&id>0;
    private static bool ValidClock(string? s)=>
        DateTimeOffset.TryParse(s,CultureInfo.InvariantCulture,DateTimeStyles.None,out _);
    private static bool ValidPlatformId(string? id)=>
        id is not null&&id.Length is >=1 and <=64&&id.All(ch=>ch is >= '0' and <= '9')&&
        long.TryParse(id,NumberStyles.None,CultureInfo.InvariantCulture,out var n)&&n>0;
    private static bool ValidDelivery(string[] fields)
    {
        try
        {
            // attempt, run, step, account, chat, topic, correlation, state, message, code, revision, created, updated
            if(fields.Length!=13||!ValidGuid(fields[0])||!ValidGuid(fields[1])||
               !ValidGuid(fields[2])||!ValidGuid(fields[3])||!ValidChat(fields[4])||
               !ValidTopic(fields[5].Length==0?null:fields[5])||!ValidGuid(fields[6])||
               !Enum.TryParse<DeliveryStatus>(fields[7],false,out var status)||
               !Enum.IsDefined(status)||!int.TryParse(fields[10],out var rev)||rev<1||
               !ValidClock(fields[11])||!ValidClock(fields[12]))
                return false;
            if(status==DeliveryStatus.Confirmed&&!ValidPlatformId(fields[8]))return false;
            if(fields[8].Length>0&&!ValidPlatformId(fields[8]))return false;
            return true;
        }
        catch(Exception){return false;}
    }
    public IReadOnlyList<QuarantinedRecord> Scan(DateTimeOffset now)
    {
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        var bad=new List<(string Source,string Id,string Reason)>();
        using(var cmd=c.CreateCommand())
        {
            cmd.Transaction=tx;
            cmd.CommandText="SELECT run_id,chat_id,topic_id,state,revision,snapshot_json FROM scheduler_simulation_jobs;";
            using var r=cmd.ExecuteReader();
            while(r.Read())
            {
                var id=r.GetString(0);
                bool valid=false;
                try
                {
                    var j=JsonSerializer.Deserialize<SchedulerJob>(r.GetString(5),Json)
                        ??throw new InvalidDataException("Empty scheduler checkpoint.");
                    SchedulerCheckpointStore.ValidateCheckpoint(j);
                    valid=j.RunId==id&&j.ChatId==r.GetString(1)&&
                        j.TopicId==(r.IsDBNull(2)?null:r.GetString(2))&&
                        j.Status.ToString()==r.GetString(3)&&j.Revision==r.GetInt32(4);
                }
                catch(Exception){valid=false;}
                if(!valid)bad.Add(("scheduler_simulation_jobs",id,"MALFORMED_CHECKPOINT"));
            }
        }
        using(var cmd=c.CreateCommand())
        {
            cmd.Transaction=tx;
            cmd.CommandText=@"SELECT attempt_id,run_id,step_key,account_id,chat_id,
                 topic_id,correlation_id,state,platform_message_id,diagnostic_code,
                 revision,created_utc,updated_utc FROM delivery_attempts;";
            using var r=cmd.ExecuteReader();
            while(r.Read())
            {
                string[] fields=new string[r.FieldCount];
                for(int i=0;i<fields.Length;i++)
                    fields[i]=r.IsDBNull(i)?"":Convert.ToString(r.GetValue(i),CultureInfo.InvariantCulture)??"";
                if(!ValidDelivery(fields))bad.Add(("delivery_attempts",fields[0],"MALFORMED_DELIVERY"));
            }
        }
        foreach(var (source,id,reason) in bad)
        {
            using var cmd=c.CreateCommand();cmd.Transaction=tx;
            cmd.CommandText=@"INSERT OR IGNORE INTO recovery_quarantine
              (source,record_id,reason,discovered_utc) VALUES($source,$id,$reason,$now);";
            cmd.Parameters.AddWithValue("$source",source);
            cmd.Parameters.AddWithValue("$id",id);
            cmd.Parameters.AddWithValue("$reason",reason);
            cmd.Parameters.AddWithValue("$now",now.ToUniversalTime().ToString("O"));
            cmd.ExecuteNonQuery();
        }
        var result=new List<QuarantinedRecord>();
        using(var cmd=c.CreateCommand())
        {
            cmd.Transaction=tx;cmd.CommandText=@"SELECT source,record_id,reason,discovered_utc
                     FROM recovery_quarantine ORDER BY source,record_id;";
            using var r=cmd.ExecuteReader();
            while(r.Read())
                result.Add(new(r.GetString(0),r.GetString(1),r.GetString(2),
                    DateTimeOffset.Parse(r.GetString(3),CultureInfo.InvariantCulture)));
        }
        tx.Commit();return result;
    }
}
