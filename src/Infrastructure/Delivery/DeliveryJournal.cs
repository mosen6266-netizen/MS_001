using System.Globalization;
using Microsoft.Data.Sqlite;
using Mosen.GroupScheduler.OutboundPolicy;
using Mosen.GroupScheduler.Persistence;
using Mosen.GroupScheduler.StateMachine;

namespace Mosen.GroupScheduler.Delivery;

/// <summary>SQLite transaction ledger. It records intent and correlated simulated receipts;
/// it NEVER invokes a sender or automatically submits/retries a Telegram message.</summary>
public sealed class DeliveryJournal
{
    private readonly AppDatabase _database;
    public DeliveryJournal(AppDatabase database)=>
        _database=database??throw new ArgumentNullException(nameof(database));
    private SqliteConnection Open()
    {
        _database.Initialize();
        var c=new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource=_database.FilePath,Mode=SqliteOpenMode.ReadWrite,
            ForeignKeys=true,Cache=SqliteCacheMode.Private,Pooling=false,DefaultTimeout=10
        }.ToString());
        c.Open();
        using var pragma=c.CreateCommand();
        pragma.CommandText="PRAGMA busy_timeout=10000;PRAGMA foreign_keys=ON;";
        pragma.ExecuteNonQuery();
        return c;
    }
    private static string Stamp(DateTimeOffset utc)=>utc.ToUniversalTime().ToString("O",CultureInfo.InvariantCulture);
    private static string Id(Guid id)=>id==Guid.Empty?throw new ArgumentException("Missing ID."):id.ToString("D");
    private static bool ValidChat(string? value)=>long.TryParse(value,NumberStyles.Integer,
        CultureInfo.InvariantCulture,out var id)&&id!=0;
    private static bool ValidTopic(string? value)=>value is null ||
        (long.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out var id)&&id>0);
    private static bool ValidMessage(string value)=>
        value.Length is >=1 and <=64&&value.All(c=>c is >= '0' and <= '9')&&
        long.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out var n)&&n>0;
    private static DeliveryAttempt FromRow(SqliteDataReader r)=>new(
        r.GetString(0),r.GetString(1),r.GetString(2),
        Guid.Parse(r.GetString(3)),r.GetString(4),r.IsDBNull(5)?null:r.GetString(5),
        r.GetString(6),Enum.Parse<DeliveryStatus>(r.GetString(7),false),
        r.IsDBNull(8)?null:r.GetString(8),r.GetString(9),r.GetInt32(10),
        DateTimeOffset.Parse(r.GetString(11),CultureInfo.InvariantCulture),
        DateTimeOffset.Parse(r.GetString(12),CultureInfo.InvariantCulture));
    private static DeliveryAttempt? Find(SqliteConnection c,SqliteTransaction tx,
        string where,string key)
    {
        using var command=c.CreateCommand();
        command.Transaction=tx;
        command.CommandText=@"SELECT attempt_id,run_id,step_key,account_id,chat_id,topic_id,
            correlation_id,state,platform_message_id,diagnostic_code,revision,created_utc,updated_utc
            FROM delivery_attempts WHERE "+where+";";
        command.Parameters.AddWithValue("$key",key);
        using var r=command.ExecuteReader();
        return r.Read()?FromRow(r):null;
    }
    public DeliveryAttempt? Get(string attemptId)
    {
        if(!Guid.TryParseExact(attemptId,"D",out var id)||id==Guid.Empty)
            throw new ArgumentException("Invalid attempt identifier.");
        using var c=Open();using var tx=c.BeginTransaction(deferred:true);
        return Find(c,tx,"attempt_id=$key",attemptId);
    }
    private static void AddEvent(SqliteConnection c,SqliteTransaction tx,DeliveryAttempt original,
        DeliveryStatus next,string code,DateTimeOffset now)
    {
        using var command=c.CreateCommand();command.Transaction=tx;
        command.CommandText=@"INSERT INTO delivery_events
          (event_id,attempt_id,from_state,to_state,event_code,event_utc)
          VALUES($id,$attempt,$from,$to,$code,$utc);";
        command.Parameters.AddWithValue("$id",Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$attempt",original.AttemptId);
        command.Parameters.AddWithValue("$from",original.State.ToString());
        command.Parameters.AddWithValue("$to",next.ToString());
        command.Parameters.AddWithValue("$code",code);
        command.Parameters.AddWithValue("$utc",Stamp(now));
        command.ExecuteNonQuery();
    }
    private static DeliveryAttempt Change(SqliteConnection c,SqliteTransaction tx,
        DeliveryAttempt initial,DeliveryStatus to,string code,DateTimeOffset now,string? messageId=null)
    {
        if(now<initial.UpdatedUtc)throw new InvalidOperationException("Clock regressed.");
        if(messageId is not null&&!ValidMessage(messageId))
            throw new ArgumentException("Invalid platform message identity.");
        using(var cmd=c.CreateCommand())
        {
            cmd.Transaction=tx;
            cmd.CommandText=@"UPDATE delivery_attempts SET state=$state,
              platform_message_id=coalesce($message,platform_message_id),
              diagnostic_code=$diagnostic,revision=revision+1,updated_utc=$utc
              WHERE attempt_id=$id AND revision=$rev;";
            cmd.Parameters.AddWithValue("$state",to.ToString());
            cmd.Parameters.AddWithValue("$message",(object?)messageId??DBNull.Value);
            cmd.Parameters.AddWithValue("$diagnostic",code);
            cmd.Parameters.AddWithValue("$utc",Stamp(now));
            cmd.Parameters.AddWithValue("$id",initial.AttemptId);
            cmd.Parameters.AddWithValue("$rev",initial.Revision);
            if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("Concurrent delivery mutation.");
        }
        AddEvent(c,tx,initial,to,code,now);
        return initial with {State=to,PlatformMessageId=messageId??initial.PlatformMessageId,
            DiagnosticCode=code,UpdatedUtc=now,Revision=initial.Revision+1};
    }

    /// <summary>Persist attempt before any future transport invocation. Duplicate run+step never
    /// mints a second attempt, even with a different caller ticket.</summary>
    public DeliveryAttempt Register(SchedulerJob job,OutboundPermit permit,DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(permit);
        if(job.Status!=JobStatus.Running||job.Phase!=JobPhase.AwaitingSend||
           job.NextStepIndex<0||job.NextStepIndex>=job.Steps.Count||
           !Guid.TryParseExact(job.RunId,"D",out var run)||run==Guid.Empty||
           !Guid.TryParseExact(permit.RunId,"D",out var pr)||pr!=run||
           permit.Token==Guid.Empty||
           job.Steps[job.NextStepIndex].StepKey!=permit.StepKey||
           job.Steps[job.NextStepIndex].AccountId!=permit.AccountId ||
           permit.ChatId!=job.ChatId||permit.TopicId!=job.TopicId ||
           !ValidChat(job.ChatId)||!ValidTopic(job.TopicId)||
           now<permit.ReservedUtc)
            throw new ArgumentException("Cannot bind an outbound ticket to this frozen message.");
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        var key=job.RunId+"|"+permit.StepKey;
        // Binding is run+step immutable: registering a different ticket cannot create a retry.
        using(var get=c.CreateCommand())
        {
            get.Transaction=tx;
            get.CommandText=@"SELECT attempt_id FROM delivery_attempts
                WHERE run_id=$run AND step_key=$step;";
            get.Parameters.AddWithValue("$run",job.RunId);
            get.Parameters.AddWithValue("$step",permit.StepKey);
            if(get.ExecuteScalar() is string already)
            {
                var existing=Find(c,tx,"attempt_id=$key",already)!;
                if(existing.CorrelationId!=Id(permit.Token))
                    throw new InvalidOperationException("Duplicate message attempt blocked.");
                tx.Commit();
                return existing;
            }
        }
        string attemptId=Guid.NewGuid().ToString("D");
        using(var cmd=c.CreateCommand())
        {
            cmd.Transaction=tx;
            cmd.CommandText=@"INSERT INTO delivery_attempts
                (attempt_id,run_id,step_key,account_id,chat_id,topic_id,
                 correlation_id,state,revision,created_utc,updated_utc)
                VALUES($id,$run,$step,$account,$chat,$topic,$correlation,'Planned',1,$utc,$utc);";
            cmd.Parameters.AddWithValue("$id",attemptId);
            cmd.Parameters.AddWithValue("$run",job.RunId);
            cmd.Parameters.AddWithValue("$step",permit.StepKey);
            cmd.Parameters.AddWithValue("$account",permit.AccountId.ToString("D"));
            cmd.Parameters.AddWithValue("$chat",permit.ChatId);
            cmd.Parameters.AddWithValue("$topic",(object?)permit.TopicId??DBNull.Value);
            cmd.Parameters.AddWithValue("$correlation",Id(permit.Token));
            cmd.Parameters.AddWithValue("$utc",Stamp(now));
            cmd.ExecuteNonQuery();
        }
        var row=Find(c,tx,"attempt_id=$key",attemptId)!;
        AddEvent(c,tx,row,DeliveryStatus.Planned,"REGISTERED",now);
        tx.Commit();
        return row;
    }

    private DeliveryOutcome Mutate(string attemptId,DateTimeOffset now,
        Func<SqliteConnection,SqliteTransaction,DeliveryAttempt,DeliveryOutcome> transform)
    {
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        var initial=Find(c,tx,"attempt_id=$key",attemptId) ??
            throw new KeyNotFoundException("Delivery attempt missing.");
        var result=transform(c,tx,initial);
        tx.Commit();return result;
    }
    public DeliveryOutcome BeforeSubmit(string attemptId,DateTimeOffset now)=>Mutate(attemptId,now,(c,t,j)=>
    {
        if(j.State==DeliveryStatus.Submitting)
            return new(false,"ALREADY_SUBMITTING",j);
        if(j.State!=DeliveryStatus.Planned)
            return new(false,"SEND_RETRY_FORBIDDEN",j);
        var newer=Change(c,t,j,DeliveryStatus.Submitting,"TRANSPORT_INTENT",now);
        return new(true,"SUBMITTING",newer);
    });
    /// <summary>A TDLib transport acknowledgment, not evidence of delivery. Late/duplicate
    /// pending callbacks cannot roll a confirmed or uncertain outcome backwards.</summary>
    public DeliveryOutcome MarkPending(string correlationId,DateTimeOffset now)=>
        MutateByCorrelation(correlationId,now,(c,t,j)=>
        {
            if(j.State is DeliveryStatus.Pending or DeliveryStatus.Confirmed)
                return new(false,"DUPLICATE_PENDING",j);
            if(j.State!=DeliveryStatus.Submitting)
                return new(false,"PENDING_REJECTED",j);
            var next=Change(c,t,j,DeliveryStatus.Pending,"TRANSPORT_ACCEPTED",now);
            return new(true,"PENDING",next);
        });
    private DeliveryOutcome MutateByCorrelation(string correlation,DateTimeOffset now,
        Func<SqliteConnection,SqliteTransaction,DeliveryAttempt,DeliveryOutcome> work)
    {
        if(!Guid.TryParseExact(correlation,"D",out var id)||id==Guid.Empty)
            throw new ArgumentException("Invalid correlation ID.");
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        var record=Find(c,tx,"correlation_id=$key",correlation) ??
            throw new KeyNotFoundException("Unknown external transaction.");
        var result=work(c,tx,record);tx.Commit();
        return result;
    }
    public DeliveryOutcome Confirm(OfficialDeliveryReceipt receipt,DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return MutateByCorrelation(receipt.CorrelationId,now,(c,t,j)=>
        {
            if(!receipt.SourceVerified||receipt.AccountId!=j.AccountId ||
               receipt.ChatId!=j.ChatId||receipt.TopicId!=j.TopicId||
               !ValidMessage(receipt.PlatformMessageId))
                return new(false,"RECEIPT_MISMATCH",j);
            if(j.State==DeliveryStatus.Confirmed)
                return new(false,j.PlatformMessageId==receipt.PlatformMessageId
                    ?"DUPLICATE_CONFIRMATION":"CONFLICTING_CONFIRMATION",j);
            if(j.State is not (DeliveryStatus.Submitting or DeliveryStatus.Pending or DeliveryStatus.Unknown))
                return new(false,"UNEXPECTED_CONFIRMATION",j);
            try
            {
                var next=Change(c,t,j,DeliveryStatus.Confirmed,"OFFICIAL_CONFIRMED",now,
                    receipt.PlatformMessageId);
                return new(true,"CONFIRMED",next);
            }
            catch(SqliteException ex) when(ex.SqliteErrorCode==19)
            {
                // Unique official message id was already recorded for another step.
                return new(false,"DUPLICATE_PLATFORM_MESSAGE",j);
            }
        });
    }
    public DeliveryOutcome ReportFailure(string correlationId,FailureCategory reason,
        DateTimeOffset now)=>MutateByCorrelation(correlationId,now,(c,t,j)=>
        {
            if(j.State is DeliveryStatus.Confirmed or DeliveryStatus.Failed or DeliveryStatus.Review)
                return new(false,"ALREADY_RESOLVED",j);
            var definite=DeliveryFailurePolicy.IsDefinitive(reason);
            if(j.State==DeliveryStatus.Unknown)
                return new(false,"UNKNOWN_REQUIRES_OFFICIAL_RECONCILIATION",j);
            var target= definite ? DeliveryStatus.Failed :
                j.State==DeliveryStatus.Planned?DeliveryStatus.Review:DeliveryStatus.Unknown;
            var next=Change(c,t,j,target,definite?"DEFINITE_NON_DELIVERY":"OUTCOME_UNKNOWN",now);
            return new(true,target.ToString().ToUpperInvariant(),next);
        });
    public IReadOnlyList<DeliveryAttempt> LoadAndQuarantineInterrupted(DateTimeOffset now)
    {
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        var all=new List<DeliveryAttempt>();
        using(var cmd=c.CreateCommand())
        {
            cmd.Transaction=tx;
            cmd.CommandText=@"SELECT attempt_id,run_id,step_key,account_id,chat_id,topic_id,
                correlation_id,state,platform_message_id,diagnostic_code,revision,created_utc,updated_utc
                FROM delivery_attempts d WHERE NOT EXISTS
                (SELECT 1 FROM recovery_quarantine q
                 WHERE q.source='delivery_attempts' AND q.record_id=d.attempt_id)
                ORDER BY created_utc,attempt_id;";
            using var rows=cmd.ExecuteReader();
            while(rows.Read())all.Add(FromRow(rows));
        }
        var updated=new List<DeliveryAttempt>();
        foreach(var j in all)
        {
            var target=j.State switch {
                DeliveryStatus.Planned=>DeliveryStatus.Review,
                DeliveryStatus.Submitting or DeliveryStatus.Pending=>DeliveryStatus.Unknown,
                _=>j.State
            };
            if(target==j.State){updated.Add(j);continue;}
            updated.Add(Change(c,tx,j,target,"RESTART_RECONCILIATION_REQUIRED",now));
        }
        tx.Commit();return updated;
    }
    public IReadOnlyList<DeliveryAttempt> ListReview()
    {
        using var c=Open();using var tx=c.BeginTransaction(deferred:true);
        var all=new List<DeliveryAttempt>();
        using var cmd=c.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText=@"SELECT attempt_id,run_id,step_key,account_id,chat_id,topic_id,
                correlation_id,state,platform_message_id,diagnostic_code,revision,created_utc,updated_utc
                FROM delivery_attempts d WHERE d.state IN ('Unknown','Review')
                AND NOT EXISTS (SELECT 1 FROM recovery_quarantine q
                  WHERE q.source='delivery_attempts' AND q.record_id=d.attempt_id)
                ORDER BY created_utc,attempt_id;";
        using var rows=cmd.ExecuteReader();
        while(rows.Read())all.Add(FromRow(rows));
        return all;
    }
    public int CountEvents(string attemptId)
    {
        using var c=Open();using var cmd=c.CreateCommand();
        cmd.CommandText="SELECT COUNT(*) FROM delivery_events WHERE attempt_id=$id;";
        cmd.Parameters.AddWithValue("$id",attemptId);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}
