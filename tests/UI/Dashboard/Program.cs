using Mosen.GroupScheduler.RuntimeDashboard;
using Mosen.GroupScheduler.Scheduler;
using Mosen.GroupScheduler.StateMachine;
using Mosen.GroupScheduler.Persistence;
using Mosen.GroupScheduler.Delivery;
using Microsoft.Data.Sqlite;

int n=0;
void Check(bool truth,string label){if(!truth)throw new Exception("FAIL "+label);n++;Console.WriteLine("PASS "+label);}
var at=new DateTimeOffset(2026,10,16,20,0,0,TimeSpan.Zero);
var account=Guid.NewGuid();var script=Guid.NewGuid().ToString("D");
var root=Path.Combine(Path.GetTempPath(),"ms001-dashboard-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
SchedulerJob Make(int i,JobStatus status=JobStatus.Running,int cursor=1)=>new(
    Guid.NewGuid().ToString("D"),"-100"+(100000+i),null,script,1,new string('a',64),
    new(){new(Guid.NewGuid().ToString("D"),account,30000,5000,15000),
          new(Guid.NewGuid().ToString("D"),account,30000,5000,15000),
          new(Guid.NewGuid().ToString("D"),account,30000,5000,15000)},
    status,status==JobStatus.Completed?JobPhase.Finished:JobPhase.WaitingBefore,
    status==JobStatus.Completed?3:cursor,18000,at,1);
DeliveryAttempt Delivery(SchedulerJob j,DeliveryStatus state)=>new(
    Guid.NewGuid().ToString("D"),j.RunId,j.Steps[j.NextStepIndex].StepKey,account,j.ChatId,
    null,Guid.NewGuid().ToString("D"),state,null,"",1,at,at);
try{
    var path=Path.Combine(root,"app.db");
    var db=new AppDatabase(path);db.Initialize();
    var reader=new RuntimeDashboardReader(db);
    var empty=reader.Read(at);
    Check(empty.Items.Count==0&&empty.Summary.Total==0,"empty dashboard shows no fictitious tasks");
    Check(empty.Mode=="offline-simulation-readonly","snapshot mode always describes simulation");
    var jobs=Enumerable.Range(1,50).Select(i=>Make(i)).ToList();
    jobs[0]=Make(1,JobStatus.Review);jobs[1]=Make(2,JobStatus.Failed);
    jobs[2]=Make(3,JobStatus.Paused);jobs[3]=Make(4,JobStatus.Completed);
    jobs[4]=Make(5,JobStatus.Stopped);
    var projected=RuntimeDashboardBuilder.Build(jobs,
        new[]{Delivery(jobs[0],DeliveryStatus.Unknown),Delivery(jobs[1],DeliveryStatus.Confirmed)},2,at);
    Check(projected.Summary.Total==50&&projected.Items.Count==50,"50 independent task rows projected");
    Check(projected.Summary.Review==1&&projected.Summary.Failed==1&&
        projected.Summary.Paused==1&&projected.Summary.Completed==1&&projected.Summary.Stopped==1,
        "all status counters mutually exclusive");
    Check(projected.Summary.Running==45&&projected.Summary.UncertainMessages==1&&
        projected.Summary.CorruptRecords==2,"running, uncertain and isolated records displayed");
    Check(projected.Items[0].IssueCode=="OUTCOME_UNKNOWN","unknown receipt overrides generic review issue");
    Check(projected.Items[1].IssueCode=="TASK_FAILED","failed task reason is distinct");
    Check(projected.Items[2].IssueCode=="USER_PAUSED","user pause remains visible");
    Check(projected.Items[3].CompletionPercent==100,"completed tasks display 100 percent");
    Check(projected.Items[5].CompletionPercent==33,"step progress uses integer bounded percentage");
    Check(projected.Items[5].NextStepNumber==2,"next message number is one-based");
    Check(projected.Items[5].RemainingMs==18000&&projected.Items[5].IsClockActive,
        "countdown based on frozen remaining duration and one time anchor");
    Check(!projected.Items[2].IsClockActive,"paused tasks do not visibly count down");
    Check(projected.Items[0].RunId==jobs[0].RunId,"stable immutable task identity retained");
    Check(projected.Items.All(x=>x.Revision==1&&x.CompletionPercent<=100),"revision and progress bounded");
    var already=Delivery(jobs[5],DeliveryStatus.Confirmed);
    var proj2=RuntimeDashboardBuilder.Build(new[]{jobs[5]},new[]{already},0,at);
    Check(proj2.Items.Single().CompletedSteps==2&&proj2.Items.Single().CompletionPercent==66,
        "correlated confirmation counts in-progress acknowledged current step exactly once");
    Check(RuntimeDashboardBuilder.Build(new[]{jobs[5]},new[]{already,already},0,at)
        .Items.Single().CompletedSteps==2,"duplicate acknowledgments never double-count completion");
    var store=new SchedulerCheckpointStore(db);
    // Store only 50 simulations in atomic batch. All use Ready for durable-creation validation.
    store.CreateBatch(jobs.Select(j=>j with {Status=JobStatus.Ready,
        Phase=JobPhase.WaitingBefore,NextStepIndex=0,RemainingMs=30000}).ToArray());
    var initial=reader.Read(at);
    Check(initial.Items.Count==50&&initial.Summary.Ready==50,"live SQLite reader returns 50 persisted local records");
    Check(initial.Items.All(x=>x.NextStepNumber==1&&x.CompletionPercent==0),
        "persisted simulation starts with zero committed progress");
    var single=reader.Read(at.AddSeconds(30));
    Check(single.Items.Count==50&&single.Summary.Ready==50,
        "read-only dashboard refresh does not start or advance the virtual scheduler");
    using(var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path}.ToString()))
    {c.Open();using var q=c.CreateCommand();
      q.CommandText="SELECT COUNT(*) FROM scheduler_simulation_jobs WHERE state='Ready';";
      Check(Convert.ToInt64(q.ExecuteScalar())==50,"read-only UI query never changes database task state");
    }
    var before=DateTime.UtcNow;
    for(int i=0;i<90;i++){
        var x=reader.Read(at.AddSeconds(i));
        if(x.Items.Count!=50)throw new Exception("Read drift");
    }
    Check(DateTime.UtcNow-before<TimeSpan.FromSeconds(30),
        "90 successive 50-row snapshots complete without unbounded refresh backlog");
    Check(RuntimeDashboardBuilder.Build(new[]{Make(9,JobStatus.Completed)},Array.Empty<DeliveryAttempt>(),0,at)
        .Summary.Completed==1,"completed state summary deterministic");
    Console.WriteLine($"SUMMARY: {n} dashboard model checks passed; 90 read refreshes, no Telegram transmission.");
}
finally {try{Directory.Delete(root,true);}catch(IOException){}}
