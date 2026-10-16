using Mosen.GroupScheduler.Notifications;
using Mosen.GroupScheduler.OutboundPolicy;
using Mosen.GroupScheduler.Scheduler;
using Mosen.GroupScheduler.StateMachine;
using Mosen.GroupScheduler.Persistence;

int count=0;
void Check(bool yes,string name){if(!yes)throw new Exception("FAIL "+name);count++;Console.WriteLine("PASS "+name);}
void Reject<T>(Action a,string name)where T:Exception {
    bool threw=false;try{a();}catch(T){threw=true;}Check(threw,name);
}
var root=Path.Combine(Path.GetTempPath(),"ms001-step22-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var now=new DateTimeOffset(2026,10,16,22,0,0,TimeSpan.Zero);
try
{
    var center=new NotificationCenter();
    var first=center.Publish(AlertCategory.TaskFailed,"task-0001",now);
    Check(first.IsNew&&first.ShouldPop,"first fault creates a single popup request");
    Check(first.Alert.Occurrences==1&&!first.Alert.Acknowledged,"initial alert visible and unacknowledged");
    for(int i=0;i<100;i++)center.Publish(AlertCategory.TaskFailed,"task-0001",now.AddSeconds(i+1));
    Check(center.Snapshot(500).Count==1,"100 repeat faults never stack 100 alerts");
    Check(center.Snapshot()[0].Occurrences==101,"duplicate alert counts are retained");
    Check(center.Publish(AlertCategory.DeliveryUncertain,"task-0001",now.AddMinutes(3)).IsNew,
        "different failure category for same safe run ID is separate diagnostic");
    Check(center.Snapshot().Count==2,"multiple relevant categories recorded");
    Check(!center.Acknowledge("bad-key"),"unknown alert key cannot be acknowledged");
    Check(center.Acknowledge(first.Alert.Key),"explicit dismissal acknowledges current alert");
    Check(center.Publish(AlertCategory.TaskFailed,"task-0001",now.AddMinutes(4)).ShouldPop==false,
        "acknowledged fault within quiet interval never opens a second popup");
    Check(center.Publish(AlertCategory.TaskFailed,"task-0001",now.AddMinutes(10)).ShouldPop,
        "persistent unresolved fault can warn again after quiet interval");
    Reject<ArgumentException>(()=>center.Publish(AlertCategory.TaskFailed,"raw phone +12345",now),
        "raw telephone and free text rejected from operational identifiers");
    Reject<ArgumentException>(()=>center.Publish(AlertCategory.TaskFailed,"../secrets",now),
        "untrusted path names cannot become history keys");
    Reject<ArgumentOutOfRangeException>(()=>new NotificationCenter(TimeSpan.FromSeconds(1)),
        "notification quiet period cannot be reduced below safety floor");
    Reject<ArgumentOutOfRangeException>(()=>center.Snapshot(1000),
        "history enumeration remains bounded");
    var concurrent=new NotificationCenter();
    Parallel.For(0,200,i=>concurrent.Publish(AlertCategory.RecoveryRequired,"same-issue",now));
    Check(concurrent.Snapshot().Count==1&&concurrent.Snapshot()[0].Occurrences==200,
        "200 concurrent identical errors deduplicate with no race");
    var volume=new NotificationCenter();
    for(int i=0;i<1000;i++)volume.Publish(AlertCategory.TaskFailed,"id-"+i,now.AddSeconds(i));
    Check(volume.Snapshot(500).Count==500,"large alert volume stays within 500 history records");
    Check(!volume.Snapshot(500).Any(x=>x.Key.EndsWith("id-0",StringComparison.Ordinal)),
        "old alerts evicted safely before overflow");
    Check(volume.Snapshot()[0].Key.EndsWith("id-999",StringComparison.Ordinal),
        "recent issues remain browsable in descending order");
    Check(NotificationCenter.Describe(AlertCategory.DeliveryUncertain).Contains("未知"),
        "unknown send results are clearly identified, not shown as failed");

    var historyFile=Path.Combine(root,"alerts.json");
    var persistence=new AlertHistoryStore(historyFile);
    Check(persistence.Load().Count==0,"new install starts with truly empty alert history");
    Check(persistence.Save(center.Snapshot(500)),"alert history written to user-local file");
    var restored=persistence.Load();
    Check(restored.Count==center.Snapshot().Count,"alerts persist across simulated restart");
    var newCenter=new NotificationCenter();newCenter.Restore(restored);
    Check(newCenter.Snapshot().Count==2,"notification UI can reload persisted redacted history");
    Reject<InvalidOperationException>(()=>newCenter.Restore(restored),
        "history cannot be appended twice by repeated startup handler");
    File.WriteAllText(historyFile,"{ corrupt");
    Check(persistence.Load().Count==0,"broken history fails closed with no stacktrace or raw display");

    var db=new AppDatabase(Path.Combine(root,"app.db"));db.Initialize();
    var store=new SchedulerCheckpointStore(db);
    var account=Guid.NewGuid(),script=Guid.NewGuid().ToString("D");
    var jobs=Enumerable.Range(1,50).Select(i=>new SchedulerJob(
        Guid.NewGuid().ToString("D"),"-100"+(10000+i),null,script,1,new string('a',64),
        new List<PlannedStep>{new(Guid.NewGuid().ToString("D"),account,30000,5000,10000)},
        JobStatus.Ready,JobPhase.WaitingBefore,0,30000,now,1)).ToArray();
    store.CreateBatch(jobs);
    var permitPolicy=new OutboundCoordinator();
    var latch=Path.Combine(root,"emergency-stop.latch");
    int factoryCount=0;
    var stop=new EmergencyStopController(permitPolicy,latch,t=>{
        Interlocked.Increment(ref factoryCount);
        return new FairScheduler(store,t);
    });
    Check(!stop.IsStopped,"new app has no pre-existing stop latch");
    var result=stop.Stop(now.AddSeconds(1));
    Check(result.PermitGateClosed&&result.PersistentLatchWritten&&result.AllSimulationsStopped,
        "emergency stop closes outbound gate, writes latch and stops all simulations");
    Check(result.Code=="STOPPED"&&File.Exists(latch),"stop result matches durable latch file");
    Check(new FairScheduler(store,now.AddSeconds(2)).Snapshot().All(j=>j.Status==JobStatus.Stopped),
        "all fifty simulation tasks stopped durably in a single batch");
    Check(factoryCount==1,"one emergency request creates only one durable scheduler stop");
    var again=stop.Stop(now.AddSeconds(5));
    Check(again.AlreadyStopped&&factoryCount==1,"duplicate tray/click request cannot run a second stop");
    var revokedJob=jobs[0] with {Status=JobStatus.Running,Phase=JobPhase.AwaitingSend};
    var proof=new SendAuthority(account,false,now,null,null,null,false,true,false);
    Check(permitPolicy.TryReserve(revokedJob,proof,now.AddSeconds(10)).Code=="SHUTDOWN",
        "outbound policy is closed regardless of stale permissions or attached groups");
    var newPolicy=new OutboundCoordinator();
    var afterRestart=new EmergencyStopController(newPolicy,latch,_=>null);
    Check(afterRestart.IsStopped,"new app instance recognizes the persistent emergency latch");
    Check(newPolicy.TryReserve(revokedJob,proof,now.AddHours(1)).Code=="SHUTDOWN",
        "restart cannot silently clear stop latch or reacquire outbound permission");
    Check(afterRestart.Stop(now.AddHours(1)).PermitGateClosed,
        "second session retains closed gate when latch already exists");

    var deniedPolicy=new OutboundCoordinator();
    var badRoot=Path.Combine(root,"latch-parent-file");
    File.WriteAllText(badRoot,"existing file");
    var denied=new EmergencyStopController(deniedPolicy,Path.Combine(badRoot,"latch"),_=>null);
    var deniedResult=denied.Stop(now);
    Check(deniedResult.PermitGateClosed&&!deniedResult.PersistentLatchWritten &&
        deniedResult.Code=="LATCH_WRITE_FAILED",
        "disk path failure cannot reopen current outbound permission gate");
    Check(deniedPolicy.TryReserve(revokedJob,proof,now).Code=="SHUTDOWN",
        "I/O failure still blocks any later send authorization");
    var retryFailure=denied.Stop(now.AddMinutes(1));
    Check(!retryFailure.PersistentLatchWritten,
        "stop failure remains explicit when local persistence still unavailable");

    Console.WriteLine($"SUMMARY: {count} Step22 notification and emergency-stop assertions passed; 50 simulations stopped, zero Telegram sends.");
}
finally {try{Directory.Delete(root,true);}catch(IOException){}}
