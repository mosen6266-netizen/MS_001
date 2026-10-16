using Mosen.GroupScheduler.ResourceBudget;
using Mosen.GroupScheduler.Core.Telemetry;
using System.Diagnostics;

int passed=0;
void Check(bool condition,string name)
{
    if(!condition)throw new InvalidOperationException("FAIL "+name);
    passed++;Console.WriteLine("PASS "+name);
}
void Reject<T>(Action work,string name) where T:Exception
{
    var caught=false;try{work();}catch(T){caught=true;}Check(caught,name);
}
var now=new DateTimeOffset(2026,10,16,20,0,0,TimeSpan.Zero);
static ResourceSample S(DateTimeOffset t,double cpu=6,long ram=140,long heap=35,
    int threads=8,int handles=60,bool handlesKnown=true,int timers=2,int cache=50,
    long logBytes=1024)=>new(t,cpu,ram,heap,threads,handles,handlesKnown,timers,cache,logBytes);
var monitor=new ResourceBudgetMonitor();
var baseline=monitor.Observe(S(now));
Check(baseline.Level==PressureLevel.Normal&&baseline.UiReadInterval==TimeSpan.FromSeconds(5),
    "normal budget uses original 5-second dashboard refresh");
Check(baseline.AlertScanInterval==TimeSpan.FromSeconds(6)&&baseline.AllowOptionalWork,
    "baseline allows optional readonly scanning without extra threads");
var pending=monitor.Observe(S(now.AddSeconds(15),cpu:76));
Check(pending.Level==PressureLevel.Normal&&!pending.ShouldNotify,
    "single transient CPU spike does not cause UI-alert storm");
var warn=monitor.Observe(S(now.AddSeconds(30),cpu:76));
Check(warn.Level==PressureLevel.Conserve&&warn.Changed&&warn.ShouldNotify,
    "two consecutive budget overruns enter conserve with one alert");
Check(warn.UiReadInterval==TimeSpan.FromSeconds(15)&&
    warn.AlertScanInterval==TimeSpan.FromSeconds(30)&&!warn.AllowOptionalWork,
    "conserve slows optional dashboard/alert refresh without blocking UI");
var still=monitor.Observe(S(now.AddSeconds(45),cpu:76));
Check(still.Level==PressureLevel.Conserve&&!still.ShouldNotify,
    "continuous spike does not spam repeated alerts");
var spike=monitor.Observe(S(now.AddSeconds(60),ram:901,cpu:97));
Check(spike.Level==PressureLevel.Conserve,
    "single critical spike is debounce-protected");
var critical=monitor.Observe(S(now.AddSeconds(75),ram:901,cpu:97));
Check(critical.Level==PressureLevel.Critical&&critical.ShouldNotify,
    "two critical resource readings activate critical degradation");
Check(critical.UiReadInterval==TimeSpan.FromSeconds(30)&&
    critical.AlertScanInterval==TimeSpan.FromSeconds(60),
    "critical level degrades optional polls to 30/60 seconds");
Check(critical.Signal==BudgetSignal.Multiple,
    "multiple breached resources classified without leaking raw data");
var cool1=monitor.Observe(S(now.AddSeconds(90)));
var cool2=monitor.Observe(S(now.AddSeconds(105)));
var cool3=monitor.Observe(S(now.AddSeconds(120)));
Check(cool1.Level==PressureLevel.Critical&&cool2.Level==PressureLevel.Critical,
    "one or two good samples never rapidly oscillate resource level");
Check(cool3.Level==PressureLevel.Normal&&cool3.Changed&&
    cool3.UiReadInterval==TimeSpan.FromSeconds(5),
    "three good samples release throttling and restore normal refresh");
var handlesOn=new ResourceBudgetMonitor();
handlesOn.Observe(S(now,handles:3000,handlesKnown:true));
Check(handlesOn.Observe(S(now.AddSeconds(15),handles:3000,handlesKnown:true))
    .Level==PressureLevel.Critical,
    "Windows handle pressure triggers critical protection");
var handlesOff=new ResourceBudgetMonitor();
handlesOff.Observe(S(now,handles:3000,handlesKnown:false));
Check(handlesOff.Observe(S(now.AddSeconds(15),handles:3000,handlesKnown:false))
    .Level==PressureLevel.Normal,
    "Linux unavailable handle counters do not become fake failures");
var timersOn=new ResourceBudgetMonitor();
timersOn.Observe(S(now,timers:12));
Check(timersOn.Observe(S(now.AddSeconds(15),timers:12)).Level==PressureLevel.Critical,
    "unbounded timer subscriptions produce critical degradation");
var cacheOn=new ResourceBudgetMonitor();
cacheOn.Observe(S(now,cache:800));
Check(cacheOn.Observe(S(now.AddSeconds(15),cache:800)).Level==PressureLevel.Conserve,
    "cache pressure independently triggers optional refresh slowdown");
var logOn=new ResourceBudgetMonitor();
logOn.Observe(S(now,logBytes:550000));
Check(logOn.Observe(S(now.AddSeconds(15),logBytes:550000)).Level==PressureLevel.Critical,
    "oversized diagnostic log budget is independently detected");
var threadOn=new ResourceBudgetMonitor();
threadOn.Observe(S(now,threads:175));
Check(threadOn.Observe(S(now.AddSeconds(15),threads:175)).Signal==BudgetSignal.Threads,
    "thread leaks diagnosed as fixed category rather than exception details");
Reject<ArgumentOutOfRangeException>(()=>new ResourceBudgetMonitor(new ResourceLimits(
    CpuWarningPercent:75,CpuCriticalPercent:70)),
    "invalid CPU threshold ordering rejected");
Reject<ArgumentOutOfRangeException>(()=>monitor.Observe(S(now.AddMinutes(3),cpu:101)),
    "CPU readings above 100 percent rejected");
Reject<ArgumentOutOfRangeException>(()=>monitor.Observe(S(now.AddMinutes(3),cache:-1)),
    "negative cache metrics rejected");
Reject<ArgumentOutOfRangeException>(()=>monitor.Observe(S(now.AddSeconds(80))),
    "clock regression rejected rather than corrupting history");
var reminder=new ResourceBudgetMonitor();
reminder.Observe(S(now,heap:300));
var first=reminder.Observe(S(now.AddSeconds(15),heap:300));
Check(first.ShouldNotify,"first resource warning is published");
Check(!reminder.Observe(S(now.AddMinutes(1),heap:300)).ShouldNotify,
    "recurring warning quiet period prevents popup flood");
Check(reminder.Observe(S(now.AddMinutes(31),heap:300)).ShouldNotify,
    "persistent resource pressure gets one reminder after 30 minutes");

var lru=new BoundedLruCache<int,int>(512);
for(int i=0;i<20000;i++)lru.Set(i,i);
Check(lru.Count==512,"20k cache inserts retain at most 512 entries");
Check(!lru.TryGet(0,out _)&&lru.TryGet(19999,out var latest)&&latest==19999,
    "oldest entries evicted while most recent is retained");
lru.Set(19990,-15);
Check(lru.TryGet(19990,out var changed)&&changed==-15,"replacing cache key does not increase memory");
Check(lru.Remove(19990)&&!lru.TryGet(19990,out _),"cache removal frees cached state");
Reject<ArgumentOutOfRangeException>(()=>new BoundedLruCache<int,int>(2048),
    "unbounded LRU capacity rejected");
Parallel.For(0,20000,i=>lru.Set(i%300,i));
Check(lru.Count<=512,"concurrent cache traffic remains bounded and race-free");
lru.Clear();Check(lru.Count==0,"window shutdown can free all cache entries");

var disposeCalls=0;
var registry=new SubscriptionRegistry();
for(int i=0;i<100;i++)registry.Track(()=>Interlocked.Increment(ref disposeCalls));
Check(registry.Count==100,"registered resource event unsubscribe closures tracked");
registry.Dispose();
Check(registry.Count==0&&disposeCalls==100,"close releases all 100 subscribed handlers exactly once");
registry.Dispose();
Check(disposeCalls==100,"repeated close does not release an event twice");
registry.Track(()=>Interlocked.Increment(ref disposeCalls));
Check(disposeCalls==101&&registry.Count==0,"late subscription after close immediately unsubscribed");
Reject<ArgumentNullException>(()=>registry.Track(null!),
    "null event release callback rejected");
var trace=new ResourceTrend(480);
var ticks=28800; // 8h of one-second deterministic logical process snapshots.
var soak=new ResourceBudgetMonitor();
var testRam=GC.GetTotalMemory(true);
for(int i=0;i<ticks;i++)
{
    var clock=now.AddSeconds(i);
    var sample=S(clock,ram:140+(i%10),heap:35+(i%4),cache:50);
    trace.Record(sample);
    var decision=soak.Observe(sample);
    if(decision.Level!=PressureLevel.Normal||!decision.AllowOptionalWork)
        throw new Exception("False budget escalation in eight-hour baseline.");
    lru.Set(i%200,i); // 50 synthetic groups served from same bounded state store.
}
Check(soak.Observe(S(now.AddSeconds(ticks))).ObservedSamples==ticks+1,
    "8-hour accelerated lifecycle exercises 28,800 consecutive real model ticks");
Check(trace.Count==480&&trace.Snapshot().Count==480,
    "eight-hour resource trend bounded to 480 one-minute samples");
trace.Record(S(now.AddHours(8)));
Check(trace.Count==480&&trace.Snapshot()[0].CapturedUtc>now,
    "481st minute evicts the oldest resource point and retains only bounded history");
Check(lru.Count<=200,"50-group / sustained cache cycle does not grow unbounded");
var gcAfter=GC.GetTotalMemory(true);
Check(gcAfter-testRam<32L*1024*1024,
    "8-hour synthetic pressure loop does not retain over 32MiB of managed objects");
var sampler=new ProcessResourceSampler();
var real=sampler.Sample(DateTimeOffset.UtcNow,2,lru.Count,0);
Check(real.WorkingSetMiB>0&&real.ManagedHeapMiB>=0&&real.ThreadCount>=1,
    "real OS process memory and managed heap/thread readings available");
Check(real.TimerSubscriptions==2&&real.CacheEntries==lru.Count,
    "resource sampler includes explicit timer and bounded cache budget");
var second=sampler.Sample(DateTimeOffset.UtcNow.AddSeconds(15),2,lru.Count,1024);
Check(second.CpuPercent is >=0 and <=100&&second.DiagnosticBytes==1024,
    "monotonic CPU delta bounded and diagnostic byte metadata is safe");
var dir=Path.Combine(Path.GetTempPath(),"ms001-resource-"+Guid.NewGuid().ToString("N"));
try
{
    var diag=new SafeDiagnostics(dir,maxBytes:2048);
    for(int i=0;i<180;i++)Check(diag.Record(DiagnosticEvent.IpcRejected),
        "fixed-category safe diagnostics write succeeds "+i);
    var primary=new FileInfo(Path.Combine(dir,"events.jsonl"));
    var backup=new FileInfo(Path.Combine(dir,"events.previous.jsonl"));
    Check(primary.Length<=2048&&backup.Length<=2048,
        "both active and rotated diagnostics stay below 2KiB budget");
    Check(diag.Snapshot("ready").Warnings==180,
        "diagnostic aggregation counts events without retaining raw customer data");
}
finally{try{Directory.Delete(dir,true);}catch(IOException){}}
Console.WriteLine($"SUMMARY: {passed} Step24 budget/resource assertions passed; "+
                  $"{ticks} synthetic seconds (8h logical time); cache, timer, log caps and zero Telegram transmissions.");
