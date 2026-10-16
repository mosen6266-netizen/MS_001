using Mosen.GroupScheduler.Delivery;
using Mosen.GroupScheduler.OutboundPolicy;
using Mosen.GroupScheduler.Persistence;
using Mosen.GroupScheduler.Scheduler;
using Mosen.GroupScheduler.StateMachine;

namespace Mosen.GroupScheduler.Recovery;

public enum RecoveryCause { StartupAfterCrash, NetworkLost, ComputerSuspending, ComputerResumed, ProcessExiting }
public sealed record RecoveryReport(RecoveryCause Cause,DateTimeOffset CheckedUtc,
    IReadOnlyList<SchedulerJob> Jobs,IReadOnlyList<DeliveryAttempt> DeliveryAttempts,
    IReadOnlyList<QuarantinedRecord> Quarantined,bool RequiresManualReview,bool CanAutomaticallySend)
{
    public int PendingReviewCount=>Jobs.Count(j=>j.Status==JobStatus.Review)+
        DeliveryAttempts.Count(j=>j.State is DeliveryStatus.Unknown or DeliveryStatus.Review)+
        Quarantined.Count;
}

/// <summary>Fail-closed recovery gate: no background work, no network transport and no
/// automated resume, even after connectivity returns or the machine wakes.</summary>
public sealed class RecoveryCoordinator
{
    private readonly FairScheduler _scheduler;
    private readonly DeliveryJournal _delivery;
    private readonly OutboundCoordinator _policy;
    private readonly IReadOnlyList<QuarantinedRecord> _quarantined;
    private bool _interrupted;
    private RecoveryReport? _last;
    private RecoveryCoordinator(FairScheduler scheduler,DeliveryJournal delivery,
        OutboundCoordinator policy,IReadOnlyList<QuarantinedRecord> quarantined)
    {
        _scheduler=scheduler;_delivery=delivery;_policy=policy;_quarantined=quarantined;
    }
    /// <summary>Startup path scans and isolates corrupt rows before opening any scheduler.
    /// The separate outbound coordinator is deliberately closed on startup.</summary>
    public static RecoveryReport OpenAfterCrash(AppDatabase database,DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(database);
        var scanner=new RecoveryQuarantineScanner(database);
        var quarantined=scanner.Scan(now);
        var scheduler=new FairScheduler(new SchedulerCheckpointStore(database),now);
        var delivery=new DeliveryJournal(database);
        var attempts=delivery.LoadAndQuarantineInterrupted(now);
        return new RecoveryReport(RecoveryCause.StartupAfterCrash,now,
            scheduler.Snapshot(),attempts,quarantined,true,false);
    }
    /// <summary>Use only in offline fault-injection or future trusted desktop lifecycle wiring.</summary>
    public static RecoveryCoordinator ForActiveSimulation(FairScheduler scheduler,
        DeliveryJournal delivery,OutboundCoordinator policy)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(policy);
        return new(scheduler,delivery,policy,Array.Empty<QuarantinedRecord>());
    }
    public bool RequiresManualReview=>_interrupted;
    public bool CanAutomaticallySend=>false;
    public RecoveryReport Interrupt(RecoveryCause cause,DateTimeOffset now)
    {
        if(cause is not (RecoveryCause.NetworkLost or RecoveryCause.ComputerSuspending or
            RecoveryCause.ProcessExiting))
            throw new ArgumentException("Only a real interruption may close an active session.");
        if(_last is not null)return _last;
        // Immediately prevent any new permit, even if durable IO below fails.
        _policy.Shutdown();
        _interrupted=true;
        _scheduler.HoldAllForReview(now); // atomic checkpoint update, closes simulator
        var attempts=_delivery.LoadAndQuarantineInterrupted(now);
        _last=new RecoveryReport(cause,now,_scheduler.Snapshot(),attempts,
            _quarantined,true,false);
        return _last;
    }
    public RecoveryReport OnConnectivityRestored(DateTimeOffset now)
    {
        if(!_interrupted||_last is null)
            throw new InvalidOperationException("Session has not entered recovery.");
        // Reconnecting / waking is not permission to restart any task.
        return _last with {Cause=RecoveryCause.ComputerResumed,CheckedUtc=now,
            CanAutomaticallySend=false,RequiresManualReview=true};
    }
}
