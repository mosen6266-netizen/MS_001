using Mosen.GroupScheduler.Persistence;
using Mosen.GroupScheduler.Groups;
using Mosen.GroupScheduler.Scripts.Editor;
using Mosen.GroupScheduler.Scripts.Preview;
using Mosen.GroupScheduler.Preflight;
using Mosen.GroupScheduler.StateMachine;
using Mosen.GroupScheduler.Scheduler;

int n=0;
void Check(bool yes,string label){if(!yes)throw new Exception("FAIL "+label);n++;Console.WriteLine("PASS "+label);}
void Reject(Action action,string label)
{
    bool fail=false;try{action();}catch(Exception e)when(e is InvalidOperationException or ArgumentException or Microsoft.Data.Sqlite.SqliteException){fail=true;}
    Check(fail,label);
}
var basePath=Path.Combine(Path.GetTempPath(),"ms001-step17-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(basePath);
try
{
    var db=new AppDatabase(Path.Combine(basePath,"app.db"));db.Initialize();
    Check(db.GetSchemaVersion()==7,"migration v7 installed without losing prior schema");
    var store=new SchedulerCheckpointStore(db);
    var clock=new DateTimeOffset(2026,10,16,19,0,0,TimeSpan.Zero);
    var role=Guid.NewGuid().ToString("D");var account=Guid.NewGuid();
    var steps=new List<EditorStep> {
        new(Guid.NewGuid().ToString("D"),role,"公告",30000,5000,12000),
        new(Guid.NewGuid().ToString("D"),role,"下一条",0,0,0),
        new(Guid.NewGuid().ToString("D"),role,"最后一条",1000,0,0)
    };
    var doc=new EditorDocument("合成虚拟时钟",new(){new(role,"客服")},steps);
    var pub=new PublishedScript(Guid.NewGuid().ToString("D"),1,1,new string('a',64),clock,doc);
    PreflightTarget Target(int index)
    {
        var id="-1000000"+index.ToString("D3");
        var group=new GroupInfo(id,"测试群"+index,"supergroup",false,clock);
        var member=new MembershipInfo(account,id,"member",true,true,0,clock);
        return new PreflightTarget(id,null,group,null,
            new[]{new PreflightAccount(account,"客服",true,member)},
            MembershipsFreshForStart:true,HasConflictingTask:false);
    }
    var valid=PreflightPlanner.Check(pub,Enumerable.Range(1,50).Select(Target).ToArray(),
        Array.Empty<RoleChoice>(),_=>true,clock);
    Check(valid.Ready&&valid.FrozenTargets.Count==50,"50 synthetic target preflights verified independently");
    var engine=new FairScheduler(store,clock);
    var created=engine.CreateSimulations(Guid.Parse(pub.ScriptId),valid,clock);
    Check(created.Count==50&&created.All(j=>j.Status==JobStatus.Ready),
        "atomic creation of 50 durable independent group runs");
    Check(engine.Snapshot().Count==50&&engine.Snapshot().All(j=>j.Steps.Count==3),
        "each group keeps three frozen ordered steps");
    Reject(()=>engine.CreateSimulations(Guid.Parse(pub.ScriptId),valid,clock),
        "capacity guard prevents accidental second batch");
    var first=created[0].RunId;var second=created[1].RunId;
    foreach(var item in created)engine.Start(item.RunId,clock);
    Check(engine.Snapshot().All(j=>j.Status==JobStatus.Running),"50 runs start manually only");
    var all=engine.Tick(clock.AddSeconds(10));
    Check(all.Count==50&&engine.Snapshot().All(j=>j.RemainingMs==20000),
        "50 independent clocks advance equally without starvation");
    var paused=engine.Pause(first,clock.AddSeconds(14));
    Check(paused.Status==JobStatus.Paused&&paused.RemainingMs==16000,
        "pause freezes precise remaining 16 seconds");
    engine.Tick(clock.AddSeconds(30));
    Check(engine.Snapshot().Single(j=>j.RunId==first).RemainingMs==16000,
        "paused group does not continue counting while others advance");
    Check(engine.Snapshot().Count(j=>j.Phase==JobPhase.Typing)==49,
        "other 49 groups reached typing independently");
    engine.Resume(first,clock.AddSeconds(50));
    engine.Tick(clock.AddSeconds(65));
    Check(engine.Snapshot().Single(j=>j.RunId==first).RemainingMs==1000,
        "resume uses new virtual baseline, not elapsed paused wall time");
    engine.Tick(clock.AddSeconds(66));
    Check(engine.Snapshot().Single(j=>j.RunId==first).Phase==JobPhase.Typing &&
          engine.Snapshot().Single(j=>j.RunId==first).RemainingMs==5000,
        "30-second pre-wait completes before 5-second typing starts");
    engine.Tick(clock.AddSeconds(71));
    Check(engine.Snapshot().Single(j=>j.RunId==first).Phase==JobPhase.AwaitingSend,
        "35 active seconds gate a send, without triggering Telegram");
    Reject(()=>engine.SimulateConfirmed(first,clock.AddSeconds(72)),
        "receipt cannot arrive before externally submitted state");
    var queued=engine.SimulateSubmitted(first,clock.AddSeconds(72));
    Check(queued.Phase==JobPhase.AwaitingReceipt,
        "simulation can explicitly indicate submission, never automatically");
    engine.Tick(clock.AddSeconds(150));
    Check(engine.Snapshot().Single(j=>j.RunId==first).Phase==JobPhase.AwaitingReceipt,
        "no receipt means no automatic retry or post-send timing");
    var ack=engine.SimulateConfirmed(first,clock.AddSeconds(150));
    Check(ack.Phase==JobPhase.WaitingAfter&&ack.RemainingMs==12000,
        "post-send 12-second wait starts from actual simulated acknowledgment");
    engine.Tick(clock.AddSeconds(157));
    Check(engine.Snapshot().Single(j=>j.RunId==first).RemainingMs==5000,
        "post-send wait correctly counts down after receipt");
    engine.Tick(clock.AddSeconds(162));
    Check(engine.Snapshot().Single(j=>j.RunId==first).NextStepIndex==1 &&
          engine.Snapshot().Single(j=>j.RunId==first).Phase==JobPhase.WaitingBefore,
        "completing post-wait advances exactly one step");
    engine.Tick(clock.AddSeconds(162));
    Check(engine.Snapshot().Single(j=>j.RunId==first).Phase==JobPhase.AwaitingSend,
        "zero wait and zero typing becomes immediately ready, never sent automatically");
    engine.SimulateSubmitted(first,clock.AddSeconds(162));
    engine.SimulateConfirmed(first,clock.AddSeconds(162));
    engine.Tick(clock.AddSeconds(162));
    Check(engine.Snapshot().Single(j=>j.RunId==first).NextStepIndex==2,
        "zero post-wait advances to third message");
    engine.Tick(clock.AddSeconds(163));
    Check(engine.Snapshot().Single(j=>j.RunId==first).Phase==JobPhase.AwaitingSend,
        "final step ready after its own 1 second pre-wait");
    engine.SimulateSubmitted(first,clock.AddSeconds(163));
    engine.SimulateConfirmed(first,clock.AddSeconds(163));
    engine.Tick(clock.AddSeconds(163));
    Check(engine.Snapshot().Single(j=>j.RunId==first).Status==JobStatus.Completed,
        "independent group completes exactly once after all three acknowledgments");
    engine.Fail(second,clock.AddSeconds(163));
    var afterFault=engine.Tick(clock.AddSeconds(164));
    Check(engine.Snapshot().Single(j=>j.RunId==second).Status==JobStatus.Failed &&
        engine.Snapshot().Count(j=>j.Status==JobStatus.Running)==48,
        "failure in one group does not stop remaining 48");
    Check(afterFault.Count==48&&afterFault.All(x=>x.RunId!=second),
        "round-robin scheduler ignores terminal failed group");
    var waiting=engine.Snapshot().First(j=>j.Status==JobStatus.Running).RunId;
    engine.ReviewUnknown(waiting,clock.AddSeconds(164));
    Check(engine.Snapshot().Single(j=>j.RunId==waiting).Status==JobStatus.Review,
        "uncertain state needs manual review and never blind retry");
    var independent=engine.Snapshot().First(j=>j.Status==JobStatus.Running).RunId;
    var before=engine.Snapshot().Single(j=>j.RunId==independent).Revision;
    engine.Tick(clock.AddSeconds(165));
    Check(engine.Snapshot().Single(j=>j.RunId==independent).Revision>before,
        "other groups keep their own persisted progress");
    // Startup from a second instance must never resume ready/running/paused tasks.
    var restarted=new FairScheduler(new SchedulerCheckpointStore(db),clock.AddDays(1));
    Check(restarted.Snapshot().Count==50,"all simulations survive process restart");
    Check(restarted.Snapshot().All(j=>j.Status is JobStatus.Review or JobStatus.Completed or JobStatus.Failed),
        "restart fails closed: unfinished groups require explicit review");
    Check(restarted.Tick(clock.AddDays(1).AddHours(1)).Count==0,
        "restart never schedules a hidden background tick or send");
    Reject(()=>restarted.Resume(independent,clock.AddDays(1).AddHours(1)),
        "review state cannot resume with a stale preflight");
    restarted.StopAll(clock.AddDays(1).AddHours(2));
    Check(restarted.IsShutdown,"global shutdown is a terminal in-process barrier");
    Check(restarted.Snapshot().All(j=>j.Terminal),"global shutdown stops every unfinished simulated group");
    Reject(()=>restarted.Start(independent,clock.AddDays(1).AddHours(3)),
        "shut-down process cannot restart tasks");
    var final=new FairScheduler(store,clock.AddDays(2));
    Check(final.Snapshot().All(j=>j.Terminal) && final.Tick(clock.AddDays(2)).Count==0,
        "shutdown checkpoints persist across restart without reactivation");
    // An invalid ready flag must not bypass existing step16 preflight.
    var bad=new PreflightResult(false,1,new string('a',64),
        new[]{new PreflightIssue("-123",null,"ROLE_MISSING","missing")},Array.Empty<FrozenTarget>());
    Reject(()=>final.CreateSimulations(Guid.Parse(pub.ScriptId),bad,clock),
        "rejected preflight cannot seed scheduler");
    Check(File.Exists(db.FilePath),"SQLite checkpoint DB stored locally");
    Console.WriteLine("SUMMARY: "+n+" Step17 scheduler assertions passed; no live Telegram calls.");
}
finally {try{Directory.Delete(basePath,true);}catch(IOException){}}
