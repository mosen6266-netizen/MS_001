using System.Text.Json;
using Microsoft.Data.Sqlite;
using Mosen.GroupScheduler.Persistence;
using Mosen.GroupScheduler.StateMachine;

namespace Mosen.GroupScheduler.Scheduler;

/// <summary>Durable simulator checkpoints only. This table is NEVER an outbound command queue.</summary>
public sealed class SchedulerCheckpointStore
{
    private readonly AppDatabase _db;
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    public SchedulerCheckpointStore(AppDatabase database) => _db=database??throw new ArgumentNullException(nameof(database));
    private SqliteConnection Open()
    {
        _db.Initialize();
        var connection=new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource=_db.FilePath,Mode=SqliteOpenMode.ReadWrite,ForeignKeys=true,
            Pooling=false,Cache=SqliteCacheMode.Private,DefaultTimeout=10
        }.ToString());
        connection.Open();
        using var pragma=connection.CreateCommand();
        pragma.CommandText="PRAGMA foreign_keys=ON;PRAGMA busy_timeout=10000;";
        pragma.ExecuteNonQuery();
        return connection;
    }
    private static void Validate(SchedulerJob j)
    {
        if(!Guid.TryParseExact(j.RunId,"D",out var id)||id==Guid.Empty ||
           !Guid.TryParseExact(j.ScriptId,"D",out var scriptId)||scriptId==Guid.Empty ||
           j.ScriptVersion<1 || j.ScriptSha256.Length!=64 ||
           j.ScriptSha256.Any(ch=>ch is not (>= '0' and <= '9' or >= 'a' and <= 'f')) ||
           !long.TryParse(j.ChatId,out var chatId)||chatId==0 ||
           (j.TopicId is not null&&(!long.TryParse(j.TopicId,out var topic)||topic<=0)) ||
           j.Steps.Count is <1 or >1000 || j.Revision<1 ||
           j.RemainingMs<0 || j.NextStepIndex<0 || j.NextStepIndex>j.Steps.Count)
            throw new ArgumentException("Invalid simulation checkpoint.");
        if(j.Steps.Any(step=>!Guid.TryParseExact(step.StepKey,"D",out var key)||key==Guid.Empty||
            step.AccountId==Guid.Empty || step.PreSendDelayMs is <0 or >3600000 ||
            step.TypingDurationMs is <0 or >60000 || step.PostSendDelayMs is <0 or >3600000) ||
            j.Steps.Select(x=>x.StepKey).Distinct(StringComparer.Ordinal).Count()!=j.Steps.Count)
            throw new ArgumentException("Invalid immutable step assignment.");
        if(j.Status==JobStatus.Completed && j.NextStepIndex!=j.Steps.Count)
            throw new ArgumentException("Completed run must have finished all steps.");
    }
    public void CreateBatch(IReadOnlyList<SchedulerJob> jobs)
    {
        if(jobs.Count is <1 or >50 || jobs.Any(x=>x.Revision!=1) ||
           jobs.Select(j=>j.ChatId+"|"+(j.TopicId??"")).Distinct().Count()!=jobs.Count)
            throw new ArgumentException("Invalid simulation batch.");
        foreach(var j in jobs)Validate(j);
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        foreach(var j in jobs)
        {
            var json=JsonSerializer.Serialize(j,Json);
            if(json.Length>524288)throw new ArgumentException("Checkpoint exceeds local storage limit.");
            using var cmd=c.CreateCommand();cmd.Transaction=tx;
            cmd.CommandText=@"INSERT INTO scheduler_simulation_jobs
              (run_id,chat_id,topic_id,state,revision,snapshot_json,updated_utc)
              VALUES($id,$chat,$topic,$state,$rev,$json,$time);";
            Add(cmd,j,json);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }
    private static void Add(SqliteCommand cmd,SchedulerJob j,string json)
    {
        cmd.Parameters.AddWithValue("$id",j.RunId);
        cmd.Parameters.AddWithValue("$chat",j.ChatId);
        cmd.Parameters.AddWithValue("$topic",(object?)j.TopicId??DBNull.Value);
        cmd.Parameters.AddWithValue("$state",j.Status.ToString());
        cmd.Parameters.AddWithValue("$rev",j.Revision);
        cmd.Parameters.AddWithValue("$json",json);
        cmd.Parameters.AddWithValue("$time",j.LastClockUtc.ToString("O"));
    }
    public void Update(SchedulerJob next,int expectedRevision)
    {
        Validate(next);
        if(expectedRevision<1||next.Revision!=expectedRevision+1)
            throw new InvalidOperationException("Checkpoint revision mismatch.");
        var json=JsonSerializer.Serialize(next,Json);
        if(json.Length>524288)throw new ArgumentException("Checkpoint exceeds local storage limit.");
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        using var cmd=c.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText=@"UPDATE scheduler_simulation_jobs
            SET state=$state,revision=$rev,snapshot_json=$json,updated_utc=$time
            WHERE run_id=$id AND chat_id=$chat AND
              ifnull(topic_id,'')=ifnull($topic,'') AND revision=$expected;";
        Add(cmd,next,json);
        cmd.Parameters.AddWithValue("$expected",expectedRevision);
        if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("Concurrent checkpoint change.");
        tx.Commit();
    }
    public void UpdateBatch(IReadOnlyList<(SchedulerJob Next,int Previous)> updates)
    {
        foreach(var (next,previous) in updates)
        {
            Validate(next);
            if(next.Revision!=previous+1)throw new InvalidOperationException("Invalid revision increment.");
        }
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        foreach(var (j,prev) in updates)
        {
            var json=JsonSerializer.Serialize(j,Json);
            if(json.Length>524288)throw new ArgumentException("Checkpoint exceeds local storage limit.");
            using var cmd=c.CreateCommand();cmd.Transaction=tx;
            cmd.CommandText=@"UPDATE scheduler_simulation_jobs
                SET state=$state,revision=$rev,snapshot_json=$json,updated_utc=$time
                WHERE run_id=$id AND revision=$expected;";
            Add(cmd,j,json);cmd.Parameters.AddWithValue("$expected",prev);
            if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("Concurrent checkpoint update.");
        }
        tx.Commit();
    }
    public IReadOnlyList<SchedulerJob> LoadFailClosed(DateTimeOffset now)
    {
        using var c=Open();
        var jobs=new List<SchedulerJob>();
        using(var cmd=c.CreateCommand())
        {
            cmd.CommandText="SELECT run_id,state,revision,snapshot_json FROM scheduler_simulation_jobs ORDER BY run_id;";
            using var rows=cmd.ExecuteReader();
            while(rows.Read())
            {
                var j=JsonSerializer.Deserialize<SchedulerJob>(rows.GetString(3),Json)
                    ??throw new InvalidDataException("Simulation checkpoint unreadable.");
                Validate(j);
                if(j.RunId!=rows.GetString(0)||j.Revision!=rows.GetInt32(2)||
                   !string.Equals(j.Status.ToString(),rows.GetString(1),StringComparison.Ordinal))
                    throw new InvalidDataException("Simulation checkpoint metadata mismatch.");
                jobs.Add(j);
            }
        }
        // Any nonterminal state is review-only after restart, even Paused. No auto-resume.
        var updates=jobs.Where(j=>j.Status is JobStatus.Ready or JobStatus.Running or JobStatus.Paused)
            .Select(j=>(Next:JobStateMachine.Review(j,now) with {Revision=j.Revision+1},Previous:j.Revision)).ToArray();
        if(updates.Length>0)
        {
            using var tx=c.BeginTransaction(deferred:false);
            foreach(var (next,previous) in updates)
            {
                var json=JsonSerializer.Serialize(next,Json);
                using var cmd=c.CreateCommand();cmd.Transaction=tx;
                cmd.CommandText=@"UPDATE scheduler_simulation_jobs
                    SET state='Review',revision=$rev,snapshot_json=$json,updated_utc=$time
                    WHERE run_id=$id AND revision=$previous;";
                cmd.Parameters.AddWithValue("$id",next.RunId);
                cmd.Parameters.AddWithValue("$rev",next.Revision);
                cmd.Parameters.AddWithValue("$previous",previous);
                cmd.Parameters.AddWithValue("$json",json);
                cmd.Parameters.AddWithValue("$time",now.ToString("O"));
                if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("Concurrent recovery mutation.");
            }
            tx.Commit();
        }
        var replaces=updates.ToDictionary(x=>x.Next.RunId,x=>x.Next);
        return jobs.Select(j=>replaces.TryGetValue(j.RunId,out var newest)?newest:j).ToArray();
    }
}
