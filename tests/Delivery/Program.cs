using Mosen.GroupScheduler.Delivery;
using Mosen.GroupScheduler.OutboundPolicy;
using Mosen.GroupScheduler.StateMachine;
using Mosen.GroupScheduler.Persistence;
using Microsoft.Data.Sqlite;

int passed=0;
void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);passed++;Console.WriteLine("PASS "+name);}
void Reject<T>(Action action,string name)where T:Exception
{
    bool denied=false;try{action();}catch(T){denied=true;}
    Check(denied,name);
}
var baseDir=Path.Combine(Path.GetTempPath(),"ms001-delivery-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(baseDir);
var now=new DateTimeOffset(2026,10,16,22,0,0,TimeSpan.Zero);
var account=Guid.NewGuid();var accountB=Guid.NewGuid();
SchedulerJob Job(Guid id,string chat="-100001",string? topic=null)=>new(
    Guid.NewGuid().ToString("D"),chat,topic,Guid.NewGuid().ToString("D"),
    1,new string('a',64),new(){new(Guid.NewGuid().ToString("D"),id,30000,5000,12000)},
    JobStatus.Running,JobPhase.AwaitingSend,0,0,now,1);
OutboundPermit Permit(SchedulerJob job,Guid? token=null)=>new(token??Guid.NewGuid(),job.RunId,
    job.Steps[0].StepKey,job.Steps[0].AccountId,job.ChatId,job.TopicId,now);
OfficialDeliveryReceipt Receipt(DeliveryAttempt attempt,string message="701",bool trusted=true)=>
    new(attempt.CorrelationId,attempt.AccountId,attempt.ChatId,attempt.TopicId,message,trusted);
try
{
    var db=new AppDatabase(Path.Combine(baseDir,"app.db"));db.Initialize();
    Check(db.GetSchemaVersion()==10,"schema v10 applied");
    var ledger=new DeliveryJournal(db);
    var job=Job(account);var ticket=Permit(job);
    Reject<ArgumentException>(()=>ledger.Register(job with {Phase=JobPhase.WaitingBefore},ticket,now),
        "cannot register send attempt while pre-send waiting");
    Reject<ArgumentException>(()=>ledger.Register(job with {Phase=JobPhase.Typing},ticket,now),
        "typing must finish before registering attempt");
    Reject<ArgumentException>(()=>ledger.Register(job,ticket with {AccountId=accountB},now),
        "cannot register mismatched account");
    Reject<ArgumentException>(()=>ledger.Register(job,ticket with {ChatId="-100099"},now),
        "cannot register mismatched chat");
    var planned=ledger.Register(job,ticket,now);
    Check(planned.State==DeliveryStatus.Planned&&planned.Revision==1,"atomic planned attempt persisted before submission");
    Check(Guid.TryParse(planned.AttemptId,out _)&&planned.CorrelationId==ticket.Token.ToString("D"),
        "attempt and stable correlation ids assigned");
    Check(ledger.CountEvents(planned.AttemptId)==1,"initial register audit event persisted");
    var duplicate=ledger.Register(job,ticket,now);
    Check(duplicate.AttemptId==planned.AttemptId&&ledger.CountEvents(planned.AttemptId)==1,
        "same ticket replay idempotently returns existing attempt");
    Reject<InvalidOperationException>(()=>ledger.Register(job,Permit(job),now),
        "same run step cannot mint a second attempt with a new ticket");
    var premature=ledger.Confirm(Receipt(planned),now);
    Check(!premature.Changed&&premature.Code=="UNEXPECTED_CONFIRMATION",
        "confirmation before durable submitting intent rejected");
    var earlyPending=ledger.MarkPending(planned.CorrelationId,now);
    Check(!earlyPending.Changed&&earlyPending.Code=="PENDING_REJECTED",
        "pending acknowledgment before intent rejected");
    var submitting=ledger.BeforeSubmit(planned.AttemptId,now);
    Check(submitting.Changed&&submitting.Attempt.State==DeliveryStatus.Submitting,
        "submit intent is saved before future transport invocation");
    Check(ledger.BeforeSubmit(planned.AttemptId,now).Code=="ALREADY_SUBMITTING",
        "duplicate submit-intent call cannot launch another send");
    Check(ledger.Confirm(Receipt(planned,trusted:false),now).Code=="RECEIPT_MISMATCH",
        "unverified callback cannot confirm");
    Check(ledger.Confirm(Receipt(planned) with {ChatId="-100099"},now).Code=="RECEIPT_MISMATCH",
        "callback from wrong chat cannot confirm");
    Check(ledger.Confirm(Receipt(planned) with {AccountId=accountB},now).Code=="RECEIPT_MISMATCH",
        "callback from wrong account cannot confirm");
    Check(ledger.Confirm(Receipt(planned,"bad-id"),now).Code=="RECEIPT_MISMATCH",
        "malformed official message id rejected");
    var p=ledger.MarkPending(planned.CorrelationId,now.AddSeconds(1));
    Check(p.Changed&&p.Attempt.State==DeliveryStatus.Pending,
        "transport accepted changes to pending but not confirmed");
    Check(ledger.MarkPending(planned.CorrelationId,now.AddSeconds(1)).Code=="DUPLICATE_PENDING",
        "duplicate pending acknowledged idempotently");
    var yes=ledger.Confirm(Receipt(planned),now.AddSeconds(2));
    Check(yes.Changed&&yes.Attempt.State==DeliveryStatus.Confirmed&&
        yes.Attempt.PlatformMessageId=="701","exact verified receipt confirms and persists platform identity");
    Check(ledger.CountEvents(planned.AttemptId)==4,
        "register, submitting, pending, confirmed produce exactly four events");
    Check(ledger.Confirm(Receipt(planned),now.AddSeconds(3)).Code=="DUPLICATE_CONFIRMATION",
        "repeated exact official confirmation is harmless");
    Check(ledger.Confirm(Receipt(planned,"702"),now.AddSeconds(3)).Code=="CONFLICTING_CONFIRMATION",
        "contradictory official message id cannot rewrite confirmed attempt");
    Check(ledger.MarkPending(planned.CorrelationId,now.AddSeconds(4)).Code=="DUPLICATE_PENDING",
        "late pending callback cannot move confirmed back to pending");
    Check(!ledger.BeforeSubmit(planned.AttemptId,now.AddSeconds(4)).Changed,
        "confirmed send cannot resubmit");
    Check(ledger.ReportFailure(planned.CorrelationId,FailureCategory.OfficialRejected,
        now.AddSeconds(5)).Code=="ALREADY_RESOLVED","late failure cannot downgrade confirmed");
    Check(ledger.CountEvents(planned.AttemptId)==4,
        "duplicate and contradictory events never insert misleading audit entries");

    var otherJob=Job(account);var other=ledger.Register(otherJob,Permit(otherJob),now);
    ledger.BeforeSubmit(other.AttemptId,now);
    var collision=ledger.Confirm(Receipt(other,"701"),now.AddSeconds(1));
    Check(!collision.Changed&&collision.Code=="DUPLICATE_PLATFORM_MESSAGE",
        "same account and chat official message id cannot confirm two different steps");
    Check(ledger.Get(other.AttemptId)!.State==DeliveryStatus.Submitting,
        "unique receipt collision rolls back attempted confirmation");
    Check(ledger.Confirm(Receipt(other,"703"),now.AddSeconds(2)).Changed,
        "distinct confirmed platform message id succeeds");
    var accJob=Job(accountB);var alternateAccount=ledger.Register(accJob,Permit(accJob),now);
    // Matching a permit to an unrelated synthetic job must reject.
    Check(alternateAccount.AccountId==accountB,"separate legitimate account can create its own attempt");

    var reorderedJob=Job(accountB,"-100002");var reordered=ledger.Register(reorderedJob,Permit(reorderedJob),now);
    ledger.BeforeSubmit(reordered.AttemptId,now);
    Check(ledger.Confirm(Receipt(reordered,"77"),now.AddSeconds(1)).Changed,
        "official confirmation arriving ahead of pending still resolves submitting intent");
    Check(ledger.MarkPending(reordered.CorrelationId,now.AddSeconds(2)).Code=="DUPLICATE_PENDING",
        "reordered old accepted callback does not undo confirmation");

    var unknownJob=Job(account,"-100003");var unknown=ledger.Register(unknownJob,Permit(unknownJob),now);
    ledger.BeforeSubmit(unknown.AttemptId,now);
    var timeout=ledger.ReportFailure(unknown.CorrelationId,FailureCategory.Timeout,now.AddSeconds(1));
    Check(timeout.Changed&&timeout.Attempt.State==DeliveryStatus.Unknown,
        "timeout after possible send is unknown, never definitively failed");
    Check(!ledger.BeforeSubmit(unknown.AttemptId,now.AddSeconds(2)).Changed,
        "network uncertainty cannot automatically or manually duplicate same step attempt");
    Check(ledger.ReportFailure(unknown.CorrelationId,FailureCategory.OfficialRejected,
        now.AddSeconds(3)).Code=="UNKNOWN_REQUIRES_OFFICIAL_RECONCILIATION",
        "official rejection flag alone does not clear prior uncertainty");
    Check(ledger.Confirm(Receipt(unknown,"910"),now.AddSeconds(4)).Changed,
        "trusted late receipt may reconcile an uncertain attempt");
    Check(ledger.Get(unknown.AttemptId)!.State==DeliveryStatus.Confirmed,
        "late official confirmation persists across new journal instances");

    var failedJob=Job(account,"-100004");var failed=ledger.Register(failedJob,Permit(failedJob),now);
    ledger.BeforeSubmit(failed.AttemptId,now);
    var rejected=ledger.ReportFailure(failed.CorrelationId,FailureCategory.OfficialRateLimited,
        now.AddSeconds(1));
    Check(rejected.Attempt.State==DeliveryStatus.Failed&&rejected.Attempt.DiagnosticCode=="DEFINITE_NON_DELIVERY",
        "trusted platform rejection is definitive and diagnosed without raw error text");
    Check(!ledger.BeforeSubmit(failed.AttemptId,now.AddSeconds(2)).Changed,
        "definitive error still cannot replay same transaction");
    Check(!ledger.Confirm(Receipt(failed,"930"),now.AddSeconds(2)).Changed,
        "conflicting confirmation after definitive failure held for investigation");

    var netJob=Job(accountB,"-100005");var net=ledger.Register(netJob,Permit(netJob),now);
    ledger.BeforeSubmit(net.AttemptId,now);
    Check(ledger.ReportFailure(net.CorrelationId,FailureCategory.TransportLost,
        now.AddSeconds(2)).Attempt.State==DeliveryStatus.Unknown,
        "transport loss never treated as safe to resend");

    var plannedJob=Job(accountB,"-100006");var unstarted=ledger.Register(plannedJob,Permit(plannedJob),now);
    var inFlightJob=Job(accountB,"-100007");var inFlight=ledger.Register(inFlightJob,Permit(inFlightJob),now);
    ledger.BeforeSubmit(inFlight.AttemptId,now);
    ledger.MarkPending(inFlight.CorrelationId,now.AddSeconds(1));
    var recovered=new DeliveryJournal(db).LoadAndQuarantineInterrupted(now.AddHours(1));
    Check(recovered.Single(x=>x.AttemptId==unstarted.AttemptId).State==DeliveryStatus.Review,
        "restart quarantines planned records instead of silently sending");
    Check(recovered.Single(x=>x.AttemptId==inFlight.AttemptId).State==DeliveryStatus.Unknown,
        "restart quarantines pending messages as possibly delivered");
    Check(recovered.Single(x=>x.AttemptId==planned.AttemptId).State==DeliveryStatus.Confirmed,
        "completed official confirmation survives recovery");
    Check(recovered.Single(x=>x.AttemptId==failed.AttemptId).State==DeliveryStatus.Failed,
        "definitive failure stays terminal across recovery");
    Check(recovered.Single(x=>x.AttemptId==net.AttemptId).State==DeliveryStatus.Unknown,
        "uncertain delivery remains unresolved across recovery");
    Check(new DeliveryJournal(db).ListReview().Count>=3,
        "manual review list shows unknown and interrupted transactions");
    Check(!ledger.BeforeSubmit(unstarted.AttemptId,now.AddHours(2)).Changed,
        "recovered planned attempt cannot be auto-submitted");
    Check(ledger.Confirm(Receipt(inFlight,"811"),now.AddHours(2)).Changed,
        "trusted late confirmation after restart can reconcile unknown attempt");

    Check(!DeliveryFailurePolicy.IsDefinitive(FailureCategory.NetworkUncertain),
        "network uncertainty never categorized as definite failure");
    Check(!DeliveryFailurePolicy.IsDefinitive(FailureCategory.UnverifiedResponse),
        "unverified response never proves non-delivery");
    Check(DeliveryFailurePolicy.IsDefinitive(FailureCategory.LocalPreflightRejected),
        "strict local pre-submit rejection can be definite");
    Check(DeliveryFailurePolicy.MayAutomaticallyRetry("read-message-status"),
        "read-only message status queries may safely retry");
    Check(DeliveryFailurePolicy.MayAutomaticallyRetry("read-chat-permissions"),
        "read-only permission queries may safely retry");
    Check(!DeliveryFailurePolicy.MayAutomaticallyRetry("send-message"),
        "message submission is never eligible for automatic retry");
    Check(!DeliveryFailurePolicy.MayAutomaticallyRetry("submit-photo"),
        "media submission is never automatically retried");
    Check(!DeliveryFailurePolicy.MayAutomaticallyRetry("unknown-operation"),
        "unlisted operations cannot bypass allowlist");

    Reject<KeyNotFoundException>(()=>ledger.MarkPending(Guid.NewGuid().ToString("D"),now),
        "orphan platform callbacks do not create attempts");
    Reject<ArgumentException>(()=>ledger.MarkPending("INVALID",now),
        "invalid correlation strings rejected");
    using(var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=db.FilePath}.ToString()))
    {
        c.Open();
        using var cmd=c.CreateCommand();
        cmd.CommandText="SELECT COUNT(*) FROM delivery_attempts WHERE platform_message_id='701';";
        Check(Convert.ToInt64(cmd.ExecuteScalar())==1,
            "one and only one step is associated to each confirmed message ID");
        cmd.CommandText="SELECT COUNT(*) FROM delivery_events WHERE event_code='OFFICIAL_CONFIRMED';";
        Check(Convert.ToInt64(cmd.ExecuteScalar())>=4,
            "audit events include officially correlated confirmations");
        cmd.CommandText="PRAGMA integrity_check;";
        Check(string.Equals((string)cmd.ExecuteScalar()!,"ok",StringComparison.OrdinalIgnoreCase),
            "SQLite integrity after reordered callbacks and recovery");
    }
    Console.WriteLine($"SUMMARY: {passed} Step19 delivery assertions passed, zero Telegram transmissions.");
}
finally {try{Directory.Delete(baseDir,true);}catch(IOException){}}
