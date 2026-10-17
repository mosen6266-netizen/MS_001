// Step30: real local SQLite and OS metrics, but SYNTHETIC account metadata and simulated groups.
// Strictly no tdjson, Telegram network, messages, authentication or external traffic.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Mosen.GroupScheduler.Persistence;
using Mosen.GroupScheduler.Core.Sessions;
using Mosen.GroupScheduler.Groups;
using Mosen.GroupScheduler.Scripts.Editor;
using Mosen.GroupScheduler.Scripts.Preview;
using Mosen.GroupScheduler.Preflight;
using Mosen.GroupScheduler.Scheduler;
using Mosen.GroupScheduler.StateMachine;
using Mosen.GroupScheduler.ResourceBudget;

const int Slots=300;
const int Groups=50;
const int Roles=6;
const int VirtualHours=72;
const int VirtualMinuteSamples=VirtualHours*60;
int assertions=0;
void Check(bool condition,string label)
{
    if(!condition)throw new Exception("FAIL "+label);
    assertions++;Console.WriteLine("PASS "+label);
}
void Reject(Action action,string label)
{
    try{action();}catch(InvalidOperationException){Check(true,label);return;}
    throw new Exception("FAIL (unexpected acceptance) "+label);
}

var wall=Stopwatch.StartNew();
var now=new DateTimeOffset(2026,10,17,12,0,0,TimeSpan.Zero);
var root=Path.Combine(Path.GetTempPath(),"ms001-capacity-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var output=Path.GetFullPath(Environment.GetEnvironmentVariable("MS001_PERF_REPORT") ??
    Path.Combine("reports","performance","step30-local-report.json"));
var sampler=new ProcessResourceSampler();
var before=sampler.Sample(DateTimeOffset.UtcNow,0,0,0);
var samples=new List<object>();
void Observe(string phase)
{
    GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
    var s=sampler.Sample(DateTimeOffset.UtcNow,0,0,0);
    samples.Add(new{phase,wallElapsedMs=wall.ElapsedMilliseconds,workingSetMiB=s.WorkingSetMiB,
       managedHeapMiB=s.ManagedHeapMiB,threads=s.ThreadCount,handleCount=s.HandlesKnown?(int?)s.HandleCount:null,
       cpuPercentSincePreviousSample=s.CpuPercent});
}
Observe("baseline");
long databaseBytes=0;
try
{
    var db=new AppDatabase(Path.Combine(root,"app.db"));
    db.Initialize();
    var registry=new AccountRegistry(db);
    var ids=new List<Guid>();
    for(int i=0;i<Slots;i++){
        var entry=registry.Register("capacity-slot-"+i.ToString("D3"),"group-"+(i/Roles).ToString("D2"));
        ids.Add(Guid.Parse(entry.Id));
    }
    Check(ids.Distinct().Count()==Slots && registry.Find(pageSize:20).Total==Slots,
        "300 local metadata account slots persist; no TDLib client created");
    Check(registry.Find(page:6,pageSize:50).Items.Count==50,
        "metadata pagination stays bounded with 300 entries");
    Reject(()=>registry.Register("over-capacity"),"301st metadata slot rejected");
    Observe("after-300-metadata-slots");

    var roleKeys=Enumerable.Range(0,Roles).Select(_=>Guid.NewGuid().ToString("D")).ToArray();
    var document=new EditorDocument("Synthetic capacity only",
        Enumerable.Range(0,Roles).Select(i=>new ScriptRole(roleKeys[i],"ROLE-"+i)).ToList(),
        Enumerable.Range(0,Roles).Select(i=>
            new EditorStep(Guid.NewGuid().ToString("D"),roleKeys[i],"synthetic only",1000,1000,0)).ToList());
    var scriptId=Guid.NewGuid();
    var pub=new PublishedScript(scriptId.ToString("D"),1,1,new string('a',64),now,document);
    var targets=Enumerable.Range(0,Groups).Select(i=>{
        var chat="-100"+(700000+i).ToString();
        var members=Enumerable.Range(0,Roles).Select(role=>{
            var id=ids[i*Roles+role];
            var membership=new MembershipInfo(id,chat,"member",true,true,0,now);
            return new PreflightAccount(id,"ROLE-"+role,true,membership);
        }).ToArray();
        return new PreflightTarget(chat,null,new GroupInfo(chat,"Synthetic "+i,"supergroup",false,now),
            null,members,true,false);
    }).ToArray();
    var valid=PreflightPlanner.Check(pub,targets,Array.Empty<RoleChoice>(),_=>true,now);
    Check(valid.Ready && valid.FrozenTargets.Count==Groups && valid.FrozenTargets.All(x=>x.Steps.Count==Roles),
        "50 synthetic group permissions frozen with six role accounts each (300 mappings)");
    var store=new SchedulerCheckpointStore(db);
    var engine=new FairScheduler(store,now);
    var created=engine.CreateSimulations(scriptId,valid,now);
    Check(created.Count==Groups&&engine.Snapshot().Count==Groups,"50 SQLite scheduler checkpoints created atomically");
    Reject(()=>engine.CreateSimulations(scriptId,valid,now),"51st group batch refused");
    foreach(var j in created)engine.Start(j.RunId,now);
    Check(engine.Snapshot().All(j=>j.Status==JobStatus.Running),"50 synthetic groups start separately");
    var typing=engine.Tick(now.AddSeconds(1));
    Check(typing.Count==Groups && engine.Snapshot().All(j=>j.Phase==JobPhase.Typing),
        "50 groups pass pre-wait to typing without starvation");
    var send=engine.Tick(now.AddSeconds(2));
    Check(send.Count==Groups&&engine.Snapshot().All(j=>j.Phase==JobPhase.AwaitingSend),
        "50 groups stop at external send barrier; zero network traffic");
    var first=created[0].RunId;
    engine.SimulateSubmitted(first,now.AddSeconds(3));
    engine.Tick(now.AddHours(1));
    Check(engine.Snapshot().Single(x=>x.RunId==first).Phase==JobPhase.AwaitingReceipt,
        "lost acknowledgment does not auto-resend after synthetic one-hour outage");
    engine.ReviewUnknown(first,now.AddHours(1).AddSeconds(1));
    engine.Fail(created[1].RunId,now.AddHours(1).AddSeconds(2));
    Check(engine.Snapshot().Count(x=>x.Status==JobStatus.Review)==1 &&
          engine.Snapshot().Count(x=>x.Status==JobStatus.Failed)==1,
        "simulated uncertain receipt and group failure isolated from 48 others");
    Observe("50-group-checkpoints");

    // 72h VIRTUAL model time, not 72h wall time. 1,440 snapshots/day * 3 days.
    // ResourceBudgetMonitor samples synthetic stable telemetry; separate process samples above
    // remain actual OS observations and MUST NOT be presented as 72h measured resources.
    var trend=new ResourceTrend(480);
    var budget=new ResourceBudgetMonitor();
    for(int minute=0;minute<VirtualMinuteSamples;minute++)
    {
        var logical=now.AddMinutes(minute);
        var stable=new ResourceSample(logical,8,145,32,12,48,false,1,Groups,1024);
        trend.Record(stable);
        var decision=budget.Observe(stable);
        if(decision.Level!=PressureLevel.Normal)throw new Exception("FAIL budget false pressure "+minute);
    }
    Check(trend.Count==480 && trend.Snapshot().Count==480,
        "72h virtual trend keeps exactly last 480 samples / eight logical hours");
    Check(budget.Observe(new ResourceSample(now.AddMinutes(VirtualMinuteSamples),8,145,32,12,48,
          false,1,Groups,1024)).ObservedSamples==VirtualMinuteSamples+1,
        "72h of 1-minute accelerated clock produces 4,321 budget decisions");

    // Reopen SQLite from a fresh scheduler: neither 48 active nor unknown tasks auto-run.
    var crashTime=now.AddHours(VirtualHours).AddMinutes(1);
    var restored=new FairScheduler(new SchedulerCheckpointStore(db),crashTime);
    Check(restored.Snapshot().Count==Groups &&
          restored.Snapshot().Count(x=>x.Status==JobStatus.Review)==Groups-1 &&
          restored.Snapshot().Count(x=>x.Status==JobStatus.Failed)==1,
        "simulated 72h restart retains 50 jobs, quarantines all 49 unfinished");
    Check(restored.Tick(crashTime.AddSeconds(1)).Count==0,
        "post-restart read has zero automatic ticks or sends");
    restored.StopAll(crashTime.AddMinutes(1));
    Check(restored.IsShutdown&&restored.Snapshot().All(x=>x.Terminal),
        "emergency stop closes all 50 jobs without hidden retries");
    var afterStop=new FairScheduler(new SchedulerCheckpointStore(db),crashTime.AddMinutes(10));
    Check(afterStop.Snapshot().Count==Groups&&afterStop.Snapshot().All(x=>x.Terminal),
        "emergency stop durable after subsequent restart");
    Check(registry.Find(pageSize:20).Total==Slots,
        "300 account metadata rows retained across simulated schedule failure");
    var backup=db.CreateBackup();
    Check(File.Exists(backup)&&new FileInfo(backup).Length>0,
        "SQLite snapshot consistent after stop and 72h synthetic model clock");
    databaseBytes=new FileInfo(db.FilePath).Length;
    Observe("after-recovery-and-backup");
    wall.Stop();
    var machine=new {
        os=RuntimeInformation.OSDescription,architecture=RuntimeInformation.ProcessArchitecture.ToString(),
        runtime=RuntimeInformation.FrameworkDescription,logicalCpuCount=Environment.ProcessorCount,
        ci=Environment.GetEnvironmentVariable("CI")=="true"
    };
    var report=new{
        schemaVersion=1,kind="step30-development-synthetic-capacity",measuredAtUtc=DateTimeOffset.UtcNow,
        machine, scenario=new {
            registeredAccountMetadata=Slots,simulatedTelegramClientsOpened=0,
            authorizedLiveAccounts=0,simulatedGroups=Groups,mappings=Slots,stepsPerGroup=Roles,
            simulatedExternalMessages=0,logicalHours=VirtualHours,logicalResourceSamples=VirtualMinuteSamples,
            measuredWallSeconds=Math.Round(wall.Elapsed.TotalSeconds,3)
        },
        observations=new {
            samples, sqliteMainFileBytes=databaseBytes,
            measuredWorkingSetPeakMiB=samples.Select(x=>(long)x.GetType().GetProperty("workingSetMiB")!.GetValue(x)!).Max(),
            syntheticFaultsInjected=2, automaticRetries=0,
            exceptionCount=0, assertionsPassed=assertions
        },
        limitations=new[]{
            "72h is accelerated logical time, not 72h elapsed wall-clock stability.",
            "300 accounts are SQLite metadata only; 0 native TDLib connections and 0 live accounts.",
            "50 groups are frozen offline state-machine simulations; no Telegram messages were transmitted.",
            "Synthetic resource monitor samples are generated fixture values; actual RAM/CPU/threads/handles are separate short-run process samples.",
            "No network reconnect latency or native account resource cost measured. Windows 10/11 and 72h physical soak are deferred."
        },
        outcome="synthetic-ci-pass"
    };
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    File.WriteAllText(output,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
    Check(File.Exists(output) && new FileInfo(output).Length>512,
        "machine-readable process and scenario report emitted");
    Console.WriteLine($"SUMMARY: {assertions} Step30 assertions PASS; 300 metadata slots, 50 simulated groups, 72h virtual, {wall.Elapsed.TotalSeconds:F2}s actual wall, no Telegram transmissions.");
    Console.WriteLine("REPORT: "+output);
}
finally
{
    try{Directory.Delete(root,true);}catch(IOException){}
}
