using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Mosen.GroupScheduler.History;
using Mosen.GroupScheduler.Persistence;
using Mosen.GroupScheduler.Scheduler;
using Mosen.GroupScheduler.StateMachine;
using Microsoft.Data.Sqlite;

int n=0;
void Check(bool ok,string label){if(!ok)throw new Exception("FAIL "+label);n++;Console.WriteLine("PASS "+label);}
void Reject<T>(Action action,string label) where T:Exception {bool yes=false;try{action();}catch(T){yes=true;}Check(yes,label);}
var root=Path.Combine(Path.GetTempPath(),"ms001-history-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var now=new DateTimeOffset(2026,10,16,22,0,0,TimeSpan.Zero);
try
{
    var db=new AppDatabase(Path.Combine(root,"app.db"));db.Initialize();
    Check(db.GetSchemaVersion()==10,"SQLite v10 history schema applied");
    var journal=new HistoryRepository(db);
    Check(journal.Summarize().Total==0&&journal.ReadPage().Items.Count==0,
        "new installation has no fictitious historical tasks");
    var source=Guid.NewGuid();
    var safe=new SafeEvent(source,HistoryKind.Scheduler,HistoryCode.TaskCompleted,
        HistorySeverity.Info,now,Guid.NewGuid());
    Check(journal.RecordSafeBatch(new[]{safe},now)==1,"insert bounded fixed-enum audit event");
    Check(journal.RecordSafeBatch(new[]{safe},now)==0,
        "repeated exact event GUID does not duplicate audit timeline");
    Reject<ArgumentException>(()=>journal.RecordSafeBatch(new[]{safe with {
        Severity=HistorySeverity.Error}},now),"code to severity contradiction rejected");
    Reject<ArgumentException>(()=>journal.RecordSafeBatch(new[]{safe with {
        Kind=HistoryKind.Delivery}},now),"wrong audit source kind rejected");
    Reject<ArgumentException>(()=>journal.RecordSafeBatch(new[]{safe with {
        SourceId=Guid.Empty}},now),"missing immutable source event key rejected");
    Reject<ArgumentOutOfRangeException>(()=>journal.RecordSafeBatch(
        Enumerable.Repeat(safe,1001).ToArray(),now),"unbounded batch input denied");
    Reject<ArgumentException>(()=>journal.ReadPage(pageSize:1000),
        "unbounded or arbitrary page sizes denied");
    Reject<ArgumentException>(()=>journal.ReadPage(beforeSeq:0),
        "invalid cursor denied");
    Reject<InvalidOperationException>(()=>journal.Prune(30,now,confirmed:false),
        "retention cleanup cannot run without user confirmation");
    Reject<ArgumentOutOfRangeException>(()=>journal.Prune(1,now,true),
        "retention only allows declared 7,30,90,180,365 days");
    Reject<InvalidOperationException>(()=>journal.ClearAll("YES",now),
        "full deletion requires explicit exact Chinese confirmation");

    // Import only statuses and fixed receipt event categories, not snapshot JSON or raw text.
    var account=Guid.NewGuid();var job=new SchedulerJob(Guid.NewGuid().ToString("D"),
       "-100655443322",null,Guid.NewGuid().ToString("D"),1,new string('a',64),
       new List<PlannedStep>{new(Guid.NewGuid().ToString("D"),account,30000,5000,10000)},
       JobStatus.Ready,JobPhase.WaitingBefore,0,30000,now,1);
    new SchedulerCheckpointStore(db).CreateBatch(new[]{job});
    var attempt=Guid.NewGuid().ToString("D");var receiptEvent=Guid.NewGuid().ToString("D");
    using(var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=db.FilePath,
        ForeignKeys=true,Pooling=false}.ToString()))
    {
        c.Open();using var tx=c.BeginTransaction();
        using(var command=c.CreateCommand())
        {
            command.Transaction=tx;
            command.CommandText=@"INSERT INTO delivery_attempts(
              attempt_id,run_id,step_key,account_id,chat_id,topic_id,correlation_id,state,
              revision,created_utc,updated_utc)
              VALUES($attempt,$run,$step,$account,$chat,NULL,$correlation,'Unknown',1,$utc,$utc);";
            command.Parameters.AddWithValue("$attempt",attempt);
            command.Parameters.AddWithValue("$run",job.RunId);
            command.Parameters.AddWithValue("$step",job.Steps[0].StepKey);
            command.Parameters.AddWithValue("$account",account.ToString("D"));
            command.Parameters.AddWithValue("$chat",job.ChatId);
            command.Parameters.AddWithValue("$correlation",Guid.NewGuid().ToString("D"));
            command.Parameters.AddWithValue("$utc",now.ToString("O"));
            Check(command.ExecuteNonQuery()==1,"synthetic unknown delivery exists for snapshot capture");
        }
        using(var command=c.CreateCommand())
        {
            command.Transaction=tx;
            command.CommandText=@"INSERT INTO delivery_events(event_id,attempt_id,from_state,to_state,event_code,event_utc)
              VALUES($id,$attempt,'Pending','Unknown','OUTCOME_UNKNOWN',$utc);";
            command.Parameters.AddWithValue("$id",receiptEvent);
            command.Parameters.AddWithValue("$attempt",attempt);
            command.Parameters.AddWithValue("$utc",now.ToString("O"));
            Check(command.ExecuteNonQuery()==1,"fixed-code delivery event exists without sending message");
        }
        tx.Commit();
    }
    Check(journal.CaptureLocalSnapshot(now)==2,"safe local import captures task status and uncertain receipt");
    Check(journal.CaptureLocalSnapshot(now)==0,"repeated refresh does not duplicate source audit events");
    Check(journal.Summarize().Total==3&&journal.Summarize().Errors==1,
        "imported failure severity counted independently from message body");
    var ctx=journal.ReadPage(contextId:Guid.Parse(job.RunId));
    Check(ctx.Items.Count==2,"context filter finds only matching original run");
    Check(ctx.Items.Any(x=>x.Code==HistoryCode.DeliveryUnknown)&&
        ctx.Items.Any(x=>x.Code==HistoryCode.TaskReady),
        "import retains exact fixed categories and state severity");
    Check(journal.ReadPage(severity:HistorySeverity.Error).Items.Count==1,
        "severity filter uses whitelisted indexed field");

    // Real 100k SQLite rows in ONE bounded memory transaction (no fake count benchmark).
    var testTime=Stopwatch.StartNew();
    using(var c=new SqliteConnection(new SqliteConnectionStringBuilder{
        DataSource=db.FilePath,Pooling=false}.ToString()))
    {
        c.Open();using var tx=c.BeginTransaction();
        using var cmd=c.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText=@"INSERT INTO history_events
            (source_key,event_utc,kind,code,severity,context_id,recorded_utc)
            VALUES($key,$utc,'Scheduler',$code,$severity,NULL,$utc);";
        var key=cmd.Parameters.Add("$key",SqliteType.Text);
        var date=cmd.Parameters.Add("$utc",SqliteType.Text);
        var code=cmd.Parameters.Add("$code",SqliteType.Text);
        var severity=cmd.Parameters.Add("$severity",SqliteType.Text);
        cmd.Prepare();
        for(int i=0;i<100000;i++)
        {
            key.Value="load:"+i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            date.Value=now.AddMinutes(-(i%85000)).ToString("O");
            bool error=i%47==0;
            code.Value=error?"TaskFailed":"TaskCompleted";
            severity.Value=error?"Error":"Info";
            if(cmd.ExecuteNonQuery()!=1)throw new Exception("Cannot insert synthetic row "+i);
        }
        tx.Commit();
    }
    testTime.Stop();
    Check(journal.Summarize().Total==100003,"100,000 actual SQLite rows queryable with prior data");
    Check(journal.Summarize().Errors>2000,"error counts include long 100k timeline");
    var page1=journal.ReadPage(10);
    Check(page1.Items.Count==10&&page1.HasMore&&page1.NextBeforeSeq is >0,
        "first history page is strictly bounded at 10 rows");
    var page2=journal.ReadPage(10,page1.NextBeforeSeq);
    Check(page2.Items.Count==10&&page1.Items[^1].Seq>page2.Items[0].Seq,
        "indexed keyset page advances monotonically with no duplicate overlap");
    var filtered=journal.ReadPage(10,severity:HistorySeverity.Error);
    Check(filtered.Items.Count==10&&filtered.Items.All(x=>x.Severity==HistorySeverity.Error),
        "filtered severity page never includes other event types");
    var before=Stopwatch.StartNew();
    long? cursor=null;long last=long.MaxValue;
    for(int i=0;i<100;i++)
    {
        var page=journal.ReadPage(10,cursor);
        if(page.Items.Count!=10||page.Items.Any(x=>x.Seq>=last))
            throw new Exception("Repeated page order invalid.");
        last=page.Items[^1].Seq;
        cursor=page.NextBeforeSeq;
    }
    before.Stop();
    Check(before.Elapsed<TimeSpan.FromSeconds(20),
        "100 history pages across 100k records render within 20s test budget");
    Console.WriteLine($"PERFORMANCE: 100,000 inserts {testTime.ElapsedMilliseconds}ms; "+
                      $"100 keyset pages {before.ElapsedMilliseconds}ms.");
    var bundle=SupportBundleBuilder.Build(journal,now);
    Check(bundle.Archive.Length>128&&bundle.Archive.Length<=256*1024,
        "support ZIP stays below strict 256KB size limit");
    Check(bundle.IncludedRows==200,"support bundle exports at most 200 event codes");
    using(var zip=new ZipArchive(new MemoryStream(bundle.Archive),ZipArchiveMode.Read))
    {
        Check(zip.Entries.Count==3&&zip.Entries.All(x=>
            new[]{"manifest.json","summary.json","timeline.jsonl"}.Contains(x.FullName)),
            "support bundle contains only three fixed-entry allowlisted documents");
        var all=new StringBuilder();
        foreach(var entry in zip.Entries)
        {
            using var reader=new StreamReader(entry.Open());
            all.Append(reader.ReadToEnd());
        }
        var contents=all.ToString();
        Check(!contents.Contains(job.ChatId,StringComparison.Ordinal)&&
            !contents.Contains(job.RunId,StringComparison.Ordinal)&&
            !contents.Contains(attempt,StringComparison.Ordinal)&&
            !contents.Contains(account.ToString("D"),StringComparison.Ordinal),
            "export omits group IDs, account IDs, internal run IDs and attempt IDs");
        Check(!contents.Contains(db.FilePath,StringComparison.OrdinalIgnoreCase)&&
            !contents.Contains("contextId",StringComparison.OrdinalIgnoreCase)&&
            !contents.Contains("source_key",StringComparison.OrdinalIgnoreCase),
            "export omits local paths and internal correlation keys");
        Check(contents.Contains(bundle.TicketId)&&contents.Contains("TaskFailed")&&
            contents.Contains("offline-derived-history"),
            "support ticket and enum-only diagnostic timeline are useful");
    }
    var old=journal.Summarize().Total;
    var retained=journal.Prune(30,now,confirmed:true);
    Check(retained.Removed>10000&&retained.Removed<old,
        "explicit 30d retention removes only old derived history rows");
    Check(retained.AuditWritten&&journal.ReadPage().Items[0].Code==HistoryCode.HistoryPurged,
        "cleanup leaves an auditable fixed-code tombstone");
    Check(journal.Summarize().Total==old-retained.Removed+1,
        "retention tally conserves exact counts");
    using(var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=db.FilePath}.ToString()))
    {
        c.Open();using var cmd=c.CreateCommand();
        cmd.CommandText="SELECT COUNT(*) FROM delivery_attempts;";
        Check(Convert.ToInt64(cmd.ExecuteScalar())==1,
            "retention must NEVER delete protected original delivery audit transactions");
        cmd.CommandText="SELECT COUNT(*) FROM delivery_events;";
        Check(Convert.ToInt64(cmd.ExecuteScalar())==1,
            "retention must NEVER delete official receipt/audit event ledger");
        cmd.CommandText="SELECT COUNT(*) FROM scheduler_simulation_jobs;";
        Check(Convert.ToInt64(cmd.ExecuteScalar())==1,
            "retention cannot erase user jobs or script checkpoints");
        cmd.CommandText="PRAGMA integrity_check;";
        Check((string?)cmd.ExecuteScalar()=="ok","SQLite remains structurally sound after 100k history entries and pruning");
    }
    var cleared=journal.ClearAll("清理历史",now);
    Check(cleared.Removed>0&&journal.Summarize().Total==1,
        "typed whole-history cleanup removes derived timeline only and keeps audit marker");
    Check(journal.ReadPage().Items.Single().Code==HistoryCode.HistoryPurged,
        "history clearing action remains visible after delete");
    var after=new HistoryRepository(db);
    Check(after.Summarize().Total==1,
        "history data retention persists correctly across new repository instance");

    var owned=Path.Combine(root,"diagnostics");
    Directory.CreateDirectory(owned);
    var active=Path.Combine(owned,"events.jsonl");
    var backup=Path.Combine(owned,"events.previous.jsonl");
    var unrelated=Path.Combine(owned,"private-session.db");
    File.WriteAllText(active,"TEST-SENSITIVE-LOCAL-LOG");
    File.WriteAllText(backup,"TEST-SENSITIVE-OLD-LOG");
    File.WriteAllText(unrelated,"SHOULD-NEVER-DELETE");
    File.SetLastWriteTimeUtc(active,now.AddDays(-120).UtcDateTime);
    File.SetLastWriteTimeUtc(backup,now.AddDays(-120).UtcDateTime);
    File.SetLastWriteTimeUtc(unrelated,now.AddDays(-150).UtcDateTime);
    var policy=new SafeDiagnosticLogRetention(owned);
    Check(policy.Preview(now.AddDays(-30)).Count==2,
        "log rotation previews only two fixed owned filenames");
    Reject<InvalidOperationException>(()=>policy.Prune(30,now,false),
        "diagnostic log deletion requires explicit confirmation");
    Check(policy.Prune(30,now,true)==2,"confirmed retention removes only expired owned logs");
    Check(File.Exists(unrelated)&&File.ReadAllText(unrelated)=="SHOULD-NEVER-DELETE",
        "retention never recursively removes unrelated sensitive application files");
    Check(policy.Prune(30,now,true)==0,"repeating maintenance is safely idempotent");
    Console.WriteLine($"SUMMARY: {n} Step23 history assertions passed; 100000 rows tested; zero Telegram transmissions.");
}
finally{try{Directory.Delete(root,true);}catch(IOException){}}
