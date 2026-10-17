// Step29 P0 cross-component offline E2E. No TDLib, HTTP, keys, Telegram sessions or actual sending.
using Mosen.GroupScheduler.Persistence;
using Mosen.GroupScheduler.Scripts.Editor;
using Mosen.GroupScheduler.Scripts.Portable;
using Mosen.GroupScheduler.Scripts.Media;
using Mosen.GroupScheduler.Scripts.Preview;
using Mosen.GroupScheduler.Preflight;
using Mosen.GroupScheduler.Scheduler;
using Mosen.GroupScheduler.Groups;
using Mosen.GroupScheduler.StateMachine;

int count=0;
void Check(bool actual,string label)
{
    if(!actual)throw new InvalidOperationException("FAIL "+label);
    count++;Console.WriteLine("PASS "+label);
}
void Reject(Action act,string label)
{
    try{act();}
    catch(Exception ex)when(ex is InvalidDataException or InvalidOperationException or ArgumentException)
    {Check(true,label);return;}
    throw new InvalidOperationException("FAIL expected rejection: "+label);
}
var root=Path.Combine(Path.GetTempPath(),"ms001-step29-e2e-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var start=new DateTimeOffset(2026,10,17,12,0,0,TimeSpan.Zero);
try
{
    var roleA=Guid.NewGuid().ToString("D");
    var roleB=Guid.NewGuid().ToString("D");
    var original=new EditorDocument("两群端到端回归",
        new(){new(roleA,"Alice"),new(roleB,"Bob")},
        new(){
            new(Guid.NewGuid().ToString("D"),roleA,"第一条",30000,5000,12000),
            new(Guid.NewGuid().ToString("D"),roleB,"第二条",1000,2000,0)
        });
    // Device A exports a script without account identifiers or sessions.
    var deviceA=new MediaAssetStore(Path.Combine(root,"device-A","assets"));
    var portable=PortableScriptPackage.Export(original,deviceA);
    var imported=PortableScriptPackage.Decode(portable);
    Check(imported.Document.Steps.Count==2&&imported.Document.Roles.Count==2,
        "E2E-01 exported script imports with two mapped roles");
    Check(imported.Document.Steps[0].PreSendDelayMs==30000 &&
          imported.Document.Steps[0].TypingDurationMs==5000 &&
          imported.Document.Steps[0].PostSendDelayMs==12000,
        "E2E-02 30s pre-wait, 5s typing and 12s post-wait stay separate");
    Reject(()=>PortableScriptPackage.Decode(new byte[256]),
        "E2E-03 malformed portable archive cannot be imported");
    var localAlice=Guid.NewGuid();
    var localBob=Guid.NewGuid();
    var extraAlice=Guid.NewGuid();
    var catalog=new[]{new LocalRemarkCandidate(localAlice.ToString("D"),"Alice"),
                        new LocalRemarkCandidate(localBob.ToString("D"),"Bob")};
    Check(RoleRemarkResolver.Resolve(imported.Document,catalog).All(x=>x.Status=="unique"),
        "E2E-04 imported roles resolve to local remarks, not exported account IDs");
    Check(RoleRemarkResolver.Resolve(imported.Document,catalog.Take(1))
        .Any(x=>x.Status=="missing"),"E2E-05 missing local role blocks automatic mapping");
    Check(RoleRemarkResolver.Resolve(imported.Document,
        catalog.Append(new LocalRemarkCandidate(extraAlice.ToString("D"),"Alice")))
        .Any(x=>x.Status=="ambiguous"),"E2E-06 duplicated remarks never silently pick an account");
    // Device B installs the imported draft and publishes an immutable snapshot to SQLite.
    var db=new AppDatabase(Path.Combine(root,"device-B","app.db"));db.Initialize();
    var bMedia=new MediaAssetStore(Path.Combine(root,"device-B","assets"));
    var installed=PortableScriptPackage.InstallMedia(imported,bMedia);
    var drafts=new EditorDraftRepository(db);
    var snapshot=drafts.CreateDocument(installed);
    var scriptId=Guid.Parse(snapshot.ScriptId);
    var publisher=new PublishedScriptRepository(db,bMedia);
    var published=publisher.Publish(scriptId,snapshot.Revision);
    Check(published.Version==1&&published.Sha256.Length==64,
        "E2E-07 imported draft commits as integrity-checked published version");
    var preview=ScriptPreviewBuilder.Build(published);
    Check(preview.Entries.Count==2&&preview.Entries[0].EarliestSendFromStepStartMs==35000,
        "E2E-08 published preview cannot dispatch before the full 35s");
    var newer=drafts.Save(scriptId,snapshot.Revision,
        installed with {Title="后续修改不会修改已发布版本"});
    Check(newer.Revision>snapshot.Revision&&publisher.Get(scriptId,1)!.Document.Title==installed.Title,
        "E2E-09 later editor changes never mutate published version");
    var groupA="-100100001";var groupB="-100100002";
    PreflightTarget Target(string id,bool fresh=true,bool conflicting=false,bool duplicate=false)
    {
        var members=new List<PreflightAccount>{
            new(localAlice,"Alice",true,new MembershipInfo(localAlice,id,"member",true,true,0,start)),
            new(localBob,"Bob",true,new MembershipInfo(localBob,id,"member",true,true,0,start))
        };
        if(duplicate)members.Add(new(extraAlice,"Alice",true,
            new MembershipInfo(extraAlice,id,"member",true,true,0,start)));
        return new(id,null,new GroupInfo(id,"授权群-"+id,"supergroup",false,start),null,
            members,MembershipsFreshForStart:fresh,HasConflictingTask:conflicting);
    }
    var targets=new[]{Target(groupA),Target(groupB)};
    var checkedPlan=PreflightPlanner.Check(published,targets,Array.Empty<RoleChoice>(),_=>true,start);
    Check(checkedPlan.Ready&&checkedPlan.FrozenTargets.Count==2 &&
          checkedPlan.FrozenTargets.All(x=>x.Bindings.Count==2&&x.Steps.Count==2),
        "E2E-10 exactly authorized group-scoped account-role plan freezes for two groups");
    Check(!PreflightPlanner.Check(published,new[]{Target(groupA,false)},Array.Empty<RoleChoice>(),_=>true,start).Ready,
        "E2E-11 stale membership cannot start a simulation");
    Check(!PreflightPlanner.Check(published,new[]{Target(groupA,true,true)},Array.Empty<RoleChoice>(),_=>true,start).Ready,
        "E2E-12 conflicting group task blocks start");
    Check(!PreflightPlanner.Check(published,new[]{Target(groupA,true,false,true)},Array.Empty<RoleChoice>(),_=>true,start).Ready,
        "E2E-13 duplicate eligible remark requires explicit operator choice");
    var chosen=PreflightPlanner.Check(published,new[]{Target(groupA,true,false,true)},
        new[]{new RoleChoice(groupA,null,roleA,extraAlice)},_=>true,start);
    Check(chosen.Ready&&chosen.FrozenTargets[0].Bindings.Any(x=>x.AccountId==extraAlice),
        "E2E-14 explicit authorized group choice resolves remark conflict");
    // All of this is a deterministic local simulation. No actual messaging API is referenced.
    var engine=new FairScheduler(new SchedulerCheckpointStore(db),start);
    Reject(()=>engine.CreateSimulations(scriptId,
        PreflightPlanner.Check(published,new[]{Target(groupA,false)},Array.Empty<RoleChoice>(),_=>true,start),start),
        "E2E-15 rejected preflight cannot create a durable task");
    var jobs=engine.CreateSimulations(scriptId,checkedPlan,start);
    Check(jobs.Count==2 && jobs.All(x=>x.Status==JobStatus.Ready),
        "E2E-16 preflight accepted two durable but initially inactive jobs");
    foreach(var job in jobs)engine.Start(job.RunId,start);
    engine.Tick(start.AddSeconds(30));
    Check(engine.Snapshot().All(x=>x.Phase==JobPhase.Typing),
        "E2E-17 30-second pre-wait completes before typing begins");
    engine.Tick(start.AddSeconds(35));
    Check(engine.Snapshot().All(x=>x.Phase==JobPhase.AwaitingSend),
        "E2E-18 ready at 35 seconds, with no actual send or auto-confirmation");
    var first=jobs[0].RunId;
    Reject(()=>engine.SimulateConfirmed(first,start.AddSeconds(35)),
        "E2E-19 a receipt cannot precede explicit simulated submission");
    engine.SimulateSubmitted(first,start.AddSeconds(36));
    Check(engine.Snapshot().Single(x=>x.RunId==first).Phase==JobPhase.AwaitingReceipt,
        "E2E-20 explicit simulated submit enters receipt wait, never network call");
    engine.Tick(start.AddSeconds(100));
    Check(engine.Snapshot().Single(x=>x.RunId==first).Phase==JobPhase.AwaitingReceipt,
        "E2E-21 unconfirmed simulation never retries itself");
    engine.SimulateConfirmed(first,start.AddSeconds(100));
    Check(engine.Snapshot().Single(x=>x.RunId==first).RemainingMs==12000,
        "E2E-22 post-wait clock starts at the confirmed simulated receipt");
    engine.Tick(start.AddSeconds(112));
    Check(engine.Snapshot().Single(x=>x.RunId==first).NextStepIndex==1,
        "E2E-23 exactly one message step advances after post-send wait");
    var backup=db.CreateBackup();
    Check(File.Exists(backup)&&new FileInfo(backup).Length>0,
        "E2E-24 SQLite backup after cross-feature events is durable");
    var recoveryCandidate=db.PrepareRestore(backup);
    Check(File.Exists(recoveryCandidate)&&recoveryCandidate!=db.FilePath,
        "E2E-25 restore candidate remains separate from live SQLite DB");
    var restarted=new FairScheduler(new SchedulerCheckpointStore(db),start.AddDays(1));
    Check(restarted.Snapshot().Count==2 &&
          restarted.Snapshot().All(x=>x.Status==JobStatus.Review),
        "E2E-26 simulated crash and restart quarantines every unfinished task");
    Check(restarted.Tick(start.AddDays(1).AddMinutes(5)).Count==0,
        "E2E-27 restarted process does not send or advance jobs in review");
    Reject(()=>restarted.Resume(first,start.AddDays(1)),
        "E2E-28 stale preflight cannot resume quarantined run");
    restarted.StopAll(start.AddDays(1).AddHours(1));
    Check(restarted.IsShutdown && restarted.Snapshot().All(x=>x.Terminal),
        "E2E-29 global stop persists without a live or resumable task");
    Check(new FairScheduler(new SchedulerCheckpointStore(db),start.AddDays(2))
        .Snapshot().All(x=>x.Terminal),
        "E2E-30 global stop survives a second process restart");
    Console.WriteLine($"SUMMARY: {count} Step29 offline cross-feature E2E assertions passed. Zero actual Telegram messages.");
}
finally
{
    try{Directory.Delete(root,true);}catch(IOException){}
}
