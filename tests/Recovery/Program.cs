using Mosen.GroupScheduler.Recovery;
using Mosen.GroupScheduler.Delivery;
using Mosen.GroupScheduler.OutboundPolicy;
using Mosen.GroupScheduler.Persistence;
using Mosen.GroupScheduler.Scheduler;
using Mosen.GroupScheduler.StateMachine;
using Mosen.GroupScheduler.Preflight;
using Microsoft.Data.Sqlite;

int passes=0;
void Check(bool yes,string label)
{
    if(!yes)throw new Exception("FAIL "+label);
    passes++;Console.WriteLine("PASS "+label);
}
void Reject<T>(Action callback,string label)where T:Exception
{
    bool threw=false;try{callback();}catch(T){threw=true;}
    Check(threw,label);
}
var dir=Path.Combine(Path.GetTempPath(),"ms001-step20-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
var now=new DateTimeOffset(2026,10,16,20,0,0,TimeSpan.Zero);
var account=Guid.NewGuid(),script=Guid.NewGuid();
var sha=new string('a',64);
FrozenTarget Target(int i)=>new("-100"+(i+100).ToString(),null,
    new[]{new FrozenBinding("role",account)},
    new[]{new FrozenStep(Guid.NewGuid().ToString("D"),account,"synthetic only",null,0,0,0)});
var plan=new PreflightResult(true,1,sha,Array.Empty<PreflightIssue>(),
    Enumerable.Range(0,3).Select(Target).ToArray());
OutboundPermit Ticket(SchedulerJob j)=>new(Guid.NewGuid(),j.RunId,
    j.Steps[j.NextStepIndex].StepKey,j.Steps[j.NextStepIndex].AccountId,j.ChatId,j.TopicId,now);
SchedulerJob Extra(string chat)=>new(Guid.NewGuid().ToString("D"),chat,null,script.ToString("D"),1,
    sha,new(){new(Guid.NewGuid().ToString("D"),account,5000,1000,1000)},
    JobStatus.Ready,JobPhase.WaitingBefore,0,5000,now,1);
SchedulerJob SendJob(string chat)=>Extra(chat) with {Status=JobStatus.Running,
    Phase=JobPhase.AwaitingSend,RemainingMs=0};
OfficialDeliveryReceipt Receipt(DeliveryAttempt a,string msg)=>
    new(a.CorrelationId,a.AccountId,a.ChatId,a.TopicId,msg,true);
try
{
    var db=new AppDatabase(Path.Combine(dir,"app.db"));db.Initialize();
    Check(db.GetSchemaVersion()==9,"SQLite v9 preserves existing schema and adds non-destructive quarantine");
    var store=new SchedulerCheckpointStore(db);
    var live=new FairScheduler(store,now);
    var created=live.CreateSimulations(script,plan,now);
    Check(created.Count==3,"three independent synthetic simulations created");
    foreach(var run in created)live.Start(run.RunId,now);
    Check(live.Snapshot().All(j=>j.Status==JobStatus.Running),
        "all simulations running before synthetic disconnect");
    live.Tick(now);
    Check(live.Snapshot().All(j=>j.Phase==JobPhase.AwaitingSend),
        "synthetic messages are only ready to submit, not actually sent");
    var journal=new DeliveryJournal(db);
    var first=live.Snapshot()[0];var p1=journal.Register(first,Ticket(first),now);
    journal.BeforeSubmit(p1.AttemptId,now);
    journal.MarkPending(p1.CorrelationId,now);
    var p2=journal.Register(live.Snapshot()[1],Ticket(live.Snapshot()[1]),now);
    Check(p2.State==DeliveryStatus.Planned,"second transaction exists but is not submitted");
    var third=live.Snapshot()[2];var p3=journal.Register(third,Ticket(third),now);
    journal.BeforeSubmit(p3.AttemptId,now);
    Check(journal.Confirm(Receipt(p3,"65001"),now).Changed,
        "third transaction already confirmed before simulated network loss");
    var policy=new OutboundCoordinator();
    var coordinator=RecoveryCoordinator.ForActiveSimulation(live,journal,policy);
    Check(!coordinator.RequiresManualReview&&!coordinator.CanAutomaticallySend,
        "even active simulator recovery layer exposes no automatic real sends");
    var result=coordinator.Interrupt(RecoveryCause.NetworkLost,now.AddSeconds(1));
    Check(result.Cause==RecoveryCause.NetworkLost&&result.RequiresManualReview &&
        !result.CanAutomaticallySend,"network-loss trigger closes session and requires manual review");
    Check(live.IsShutdown&&live.Snapshot().All(x=>x.Status==JobStatus.Review),
        "every nonterminal group is atomically held for review on disconnect");
    Check(result.DeliveryAttempts.Single(x=>x.AttemptId==p1.AttemptId).State==DeliveryStatus.Unknown,
        "inflight pending message becomes uncertain instead of retryable");
    Check(result.DeliveryAttempts.Single(x=>x.AttemptId==p2.AttemptId).State==DeliveryStatus.Review,
        "planned but not submitted message is held for review");
    Check(result.DeliveryAttempts.Single(x=>x.AttemptId==p3.AttemptId).State==DeliveryStatus.Confirmed,
        "acknowledged message remains confirmed after disconnect");
    Check(result.PendingReviewCount>=5,"recovery report counts groups and uncertain messages");
    Check(policy.TryReserve(first,new SendAuthority(account,false,now,null,null,null,false,false,false),
        now.AddMinutes(1)).Code=="SHUTDOWN","outbound policy closes even before permission gate");
    Reject<InvalidOperationException>(()=>live.Tick(now.AddMinutes(2)),
        "disconnected scheduler rejects future time ticks");
    Reject<InvalidOperationException>(()=>live.Resume(first.RunId,now.AddMinutes(2)),
        "disconnected scheduler refuses silent resume");
    var connected=coordinator.OnConnectivityRestored(now.AddMinutes(3));
    Check(connected.RequiresManualReview&&!connected.CanAutomaticallySend&&
        connected.Cause==RecoveryCause.ComputerResumed,
        "network recovery alone never grants execution");
    Check(coordinator.Interrupt(RecoveryCause.ProcessExiting,now.AddMinutes(4)).PendingReviewCount==
        result.PendingReviewCount,"repeated interruption idempotently preserves first report");
    Reject<ArgumentException>(()=>coordinator.Interrupt(RecoveryCause.ComputerResumed,now),
        "resume is not an interruption that could restart execution");
    var afterCrash=RecoveryCoordinator.OpenAfterCrash(db,now.AddHours(1));
    Check(afterCrash.Jobs.Count==3&&afterCrash.Jobs.All(j=>j.Status==JobStatus.Review),
        "process restart reloads only quarantined-for-review synthetic jobs");
    Check(afterCrash.DeliveryAttempts.Single(a=>a.AttemptId==p1.AttemptId).State==DeliveryStatus.Unknown,
        "indeterminate transaction survives process crash");
    Check(afterCrash.DeliveryAttempts.Single(a=>a.AttemptId==p3.AttemptId).State==DeliveryStatus.Confirmed,
        "definitely confirmed receipt survives process crash");
    Check(!afterCrash.CanAutomaticallySend,"post-crash report cannot authorize background sends");
    Check(journal.Confirm(Receipt(p1,"65002"),now.AddHours(1).AddSeconds(1)).Changed,
        "later verified and correlated receipt safely resolves unknown transaction");
    Check(journal.Get(p1.AttemptId)!.State==DeliveryStatus.Confirmed,
        "recovered unknown marked confirmed with no new submission");

    // Inject malformed snapshots without deleting their source rows.
    var extraGood=Extra("-1009001");var extraBad=Extra("-1009002");
    store.CreateBatch(new[]{extraGood,extraBad});
    var badDeliveryJob=SendJob("-1009003");
    var corrupt=journal.Register(badDeliveryJob,Ticket(badDeliveryJob),now.AddSeconds(5));
    journal.BeforeSubmit(corrupt.AttemptId,now.AddSeconds(5));
    using(var c=new SqliteConnection(new SqliteConnectionStringBuilder{
        DataSource=db.FilePath,ForeignKeys=true,Pooling=false}.ToString()))
    {
        c.Open();
        using var cmd=c.CreateCommand();
        cmd.CommandText="UPDATE scheduler_simulation_jobs SET snapshot_json='not json' WHERE run_id=$run;";
        cmd.Parameters.AddWithValue("$run",extraBad.RunId);
        Check(cmd.ExecuteNonQuery()==1,"fault injection damages one checkpoint without deleting row");
        cmd.Parameters.Clear();
        cmd.CommandText="UPDATE delivery_attempts SET account_id='broken-guid' WHERE attempt_id=$id;";
        cmd.Parameters.AddWithValue("$id",corrupt.AttemptId);
        Check(cmd.ExecuteNonQuery()==1,"fault injection damages one delivery binding without deleting row");
    }
    var resumed=RecoveryCoordinator.OpenAfterCrash(db,now.AddHours(2));
    Check(resumed.Quarantined.Count==2,"startup identifies two corrupt rows independently");
    Check(resumed.Quarantined.Any(q=>q.Source=="scheduler_simulation_jobs"&&q.RecordId==extraBad.RunId),
        "corrupt scheduler metadata added to quarantine");
    Check(resumed.Quarantined.Any(q=>q.Source=="delivery_attempts"&&q.RecordId==corrupt.AttemptId),
        "corrupt delivery metadata added to quarantine");
    Check(resumed.Jobs.Any(j=>j.RunId==extraGood.RunId&&j.Status==JobStatus.Review),
        "valid jobs remain readable and fail closed despite another corrupted job");
    Check(!resumed.Jobs.Any(j=>j.RunId==extraBad.RunId),
        "quarantined corrupt scheduler job never becomes active");
    Check(!resumed.DeliveryAttempts.Any(a=>a.AttemptId==corrupt.AttemptId),
        "corrupt delivery record excluded from normal recovery path");
    Check(resumed.DeliveryAttempts.Any(a=>a.AttemptId==p3.AttemptId &&
        a.State==DeliveryStatus.Confirmed),"unrelated confirmed delivery remains preserved");
    Check(!journal.ListReview().Any(a=>a.AttemptId==corrupt.AttemptId),
        "malformed delivery not returned as an actionable pending send");
    using(var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=db.FilePath}.ToString()))
    {
        c.Open();using var cmd=c.CreateCommand();
        cmd.CommandText="SELECT snapshot_json FROM scheduler_simulation_jobs WHERE run_id=$run;";
        cmd.Parameters.AddWithValue("$run",extraBad.RunId);
        Check(string.Equals(cmd.ExecuteScalar() as string,"not json",StringComparison.Ordinal),
            "original damaged scheduler payload preserved for local diagnosis");
        cmd.Parameters.Clear();
        cmd.CommandText="SELECT account_id FROM delivery_attempts WHERE attempt_id=$id;";
        cmd.Parameters.AddWithValue("$id",corrupt.AttemptId);
        Check((string?)cmd.ExecuteScalar()=="broken-guid",
            "original damaged delivery data preserved for local diagnosis");
        cmd.Parameters.Clear();
        cmd.CommandText="SELECT COUNT(*) FROM recovery_quarantine;";
        Check(Convert.ToInt64(cmd.ExecuteScalar())==2,
            "quarantine preserves record IDs and does not duplicate entries");
        cmd.CommandText="PRAGMA integrity_check;";
        Check((string?)cmd.ExecuteScalar()=="ok","database remains structurally sound after isolation");
    }
    var secondRecovery=RecoveryCoordinator.OpenAfterCrash(db,now.AddHours(3));
    Check(secondRecovery.Quarantined.Count==2,
        "repeating recovery produces no duplicate quarantine rows or hidden work");
    Check(secondRecovery.Jobs.All(j=>j.Status is JobStatus.Review or JobStatus.Completed or
        JobStatus.Stopped or JobStatus.Failed),
        "recovered scheduler never returns a live Running or Ready job");
    Check(secondRecovery.DeliveryAttempts.All(a=>a.State is not
        (DeliveryStatus.Planned or DeliveryStatus.Submitting or DeliveryStatus.Pending)),
        "recovered delivery journal has no directly retryable state");

    var dir2=Path.Combine(dir,"sleep.db");var sleepDb=new AppDatabase(dir2);
    sleepDb.Initialize();
    var sleepScheduler=new FairScheduler(new SchedulerCheckpointStore(sleepDb),now);
    var sleepJobs=sleepScheduler.CreateSimulations(script,plan,now);
    sleepScheduler.Start(sleepJobs[0].RunId,now);
    var sleepPolicy=new OutboundCoordinator();
    var sleep=RecoveryCoordinator.ForActiveSimulation(sleepScheduler,new DeliveryJournal(sleepDb),sleepPolicy);
    var suspended=sleep.Interrupt(RecoveryCause.ComputerSuspending,now.AddSeconds(5));
    Check(suspended.Jobs.Single(j=>j.RunId==sleepJobs[0].RunId).Status==JobStatus.Review,
        "computer suspend atomically pauses active simulation for review");
    Check(!sleep.OnConnectivityRestored(now.AddDays(1)).CanAutomaticallySend,
        "24-hour sleep/wake cannot backfill delayed messages automatically");
    var sleepAfter=RecoveryCoordinator.OpenAfterCrash(sleepDb,now.AddDays(1));
    Check(sleepAfter.Jobs.All(j=>j.Status==JobStatus.Review),
        "unstarted jobs also remain fail closed after computer wake");
    Reject<InvalidOperationException>(()=>sleepScheduler.Start(sleepJobs[1].RunId,now.AddDays(1)),
        "sleep closes scheduler instance permanently");

    Check(!DeliveryFailurePolicy.MayAutomaticallyRetry("send-message"),
        "recovery is not allowed to retry non-idempotent send requests");
    Console.WriteLine($"SUMMARY: {passes} Step20 recovery assertions passed. No Telegram transmissions.");
}
finally {try{Directory.Delete(dir,true);}catch(IOException){}}
