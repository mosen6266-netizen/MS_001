using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Mosen.GroupScheduler.Delivery;
using Mosen.GroupScheduler.Persistence;
using Mosen.GroupScheduler.Scheduler;
using Mosen.GroupScheduler.StateMachine;

namespace Mosen.GroupScheduler.RuntimeDashboard;

/// <summary>Single read-only SQLite transaction. Never calls startup recovery (which mutates states),
/// never polls TDLib, never starts task execution, never invents runtime records.</summary>
public sealed class RuntimeDashboardReader
{
    private readonly AppDatabase _database;
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    public RuntimeDashboardReader(AppDatabase database) =>
        _database=database??throw new ArgumentNullException(nameof(database));
    public RuntimeDashboardSnapshot Read(DateTimeOffset now)
    {
        _database.Initialize();
        using var c=new SqliteConnection(new SqliteConnectionStringBuilder{
            DataSource=_database.FilePath,Mode=SqliteOpenMode.ReadOnly,Pooling=false,
            Cache=SqliteCacheMode.Private,ForeignKeys=true,DefaultTimeout=10}.ToString());
        c.Open();using var tx=c.BeginTransaction(deferred:true);
        var jobs=new List<SchedulerJob>();
        using(var cmd=c.CreateCommand())
        {
            cmd.Transaction=tx;
            cmd.CommandText=@"SELECT s.run_id,s.state,s.revision,s.snapshot_json FROM
              scheduler_simulation_jobs s
              WHERE NOT EXISTS(SELECT 1 FROM recovery_quarantine q
               WHERE q.source='scheduler_simulation_jobs' AND q.record_id=s.run_id)
              ORDER BY s.updated_utc DESC,s.run_id DESC LIMIT 10000;";
            using var r=cmd.ExecuteReader();
            while(r.Read())
            {
                try
                {
                    var j=JsonSerializer.Deserialize<SchedulerJob>(r.GetString(3),Json);
                    if(j is null)continue;
                    SchedulerCheckpointStore.ValidateCheckpoint(j);
                    if(j.RunId!=r.GetString(0)||j.Status.ToString()!=r.GetString(1)||
                        j.Revision!=r.GetInt32(2))continue;
                    jobs.Add(j);
                }
                catch(Exception ex) when(ex is JsonException or InvalidDataException or ArgumentException
                    or InvalidOperationException) { /* invalid rows never appear as active */ }
            }
        }
        var attempts=new List<DeliveryAttempt>();
        using(var cmd=c.CreateCommand())
        {
            cmd.Transaction=tx;
            // Only read the fields needed by the projection; malformed old rows are excluded.
            cmd.CommandText=@"SELECT d.run_id,d.step_key,d.state FROM delivery_attempts d
                WHERE NOT EXISTS(SELECT 1 FROM recovery_quarantine q
                  WHERE q.source='delivery_attempts' AND q.record_id=d.attempt_id)
                ORDER BY d.updated_utc DESC LIMIT 100000;";
            using var r=cmd.ExecuteReader();
            while(r.Read())
            {
                if(!Enum.TryParse<DeliveryStatus>(r.GetString(2),false,out var st)||
                    !Enum.IsDefined(st))continue;
                attempts.Add(new("",r.GetString(0),r.GetString(1),Guid.Empty,"",null,"",st,
                    null,"",1,now,now));
            }
        }
        int corrupt;
        using(var cmd=c.CreateCommand())
        {
            cmd.Transaction=tx;cmd.CommandText="SELECT COUNT(*) FROM recovery_quarantine;";
            corrupt=Convert.ToInt32(cmd.ExecuteScalar(),CultureInfo.InvariantCulture);
        }
        tx.Commit();
        return RuntimeDashboardBuilder.Build(jobs,attempts,corrupt,now);
    }
}
