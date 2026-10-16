using Mosen.GroupScheduler.OutboundPolicy;
using Mosen.GroupScheduler.StateMachine;
using Mosen.GroupScheduler.Groups;
using System.Threading;

int count=0;
void Check(bool ok,string text){if(!ok)throw new Exception("FAIL "+text);Interlocked.Increment(ref count);Console.WriteLine("PASS "+text);}
void Reject(Action action,string name)
{
    bool failed=false;try{action();}catch(ArgumentException){failed=true;}
    Check(failed,name);
}
var now=new DateTimeOffset(2026,10,16,20,0,0,TimeSpan.Zero);
var first=Guid.NewGuid();var second=Guid.NewGuid();var third=Guid.NewGuid();
SchedulerJob Job(Guid account,string chat="-100001",string? topic=null,int index=0)=>
    new(Guid.NewGuid().ToString("D"),chat,topic,Guid.NewGuid().ToString("D"),
        1,new string('a',64),
        new(){new(Guid.NewGuid().ToString("D"),account,30000,5000,12000)},
        JobStatus.Running,JobPhase.AwaitingSend,index,0,now,1);
SendAuthority Proof(Guid account,string chat="-100001",string? topic=null,
    bool authorized=true,bool fresh=true,bool canText=true,bool canPhoto=true,
    int slow=0,DateTimeOffset? authTime=null,DateTimeOffset? permissionTime=null)=>
    new(account,authorized,authTime??now,
        new GroupInfo(chat,"Group","supergroup",topic is not null,now),
        topic is null?null:new TopicInfo(chat,topic,"Topic",false,false,now),
        new MembershipInfo(account,chat,"member",canText,canPhoto,slow,permissionTime??now),
        fresh,true,false);

var manager=new OutboundCoordinator();
var j=Job(first);var proof=Proof(first);
Check(manager.TryReserve(j with {Phase=JobPhase.WaitingBefore},proof,now).Code=="NOT_READY",
    "pre-wait cannot take outbound reservation");
Check(manager.TryReserve(j with {Phase=JobPhase.Typing},proof,now).Code=="NOT_READY",
    "typing phase cannot take outbound reservation");
Check(manager.TryReserve(j,Proof(second),now).Code=="BINDING_MISMATCH",
    "frozen account ID cannot be silently replaced");
Check(manager.TryReserve(j,Proof(first,"-100002"),now).Code=="BINDING_MISMATCH",
    "cross-chat authorization cannot be reused");
Check(manager.TryReserve(j,Proof(first,authorized:false),now).Code=="AUTH_REFRESH_REQUIRED",
    "disconnected account blocked even if member data says allowed");
Check(manager.TryReserve(j,Proof(first,fresh:false),now).Code=="AUTH_REFRESH_REQUIRED",
    "cached permission without send-time refresh blocked");
Check(manager.TryReserve(j,Proof(first,authTime:now.AddSeconds(-31)),now).Code=="AUTH_REFRESH_REQUIRED",
    "authorization older than 30 seconds blocked");
Check(manager.TryReserve(j,Proof(first,authTime:now.AddSeconds(1)),now).Code=="AUTH_REFRESH_REQUIRED",
    "authorization from the future blocked");
Check(manager.TryReserve(j,Proof(first,permissionTime:now.AddMinutes(-2)),now).Code=="RECHECK_REQUIRED",
    "stale membership permission blocked");
Check(manager.TryReserve(j,Proof(first,canText:false),now).Code=="NO_PERMISSION",
    "no text permission blocked");
Check(manager.TryReserve(j,new SendAuthority(first,true,now,
    new GroupInfo("-100001","channel","channel",false,now),null,
    new MembershipInfo(first,"-100001","member",true,true,0,now),true,true,false),now).Code=="INVALID_TARGET",
    "channels cannot be substituted for confirmed groups");
Check(manager.TryReserve(j,Proof(first) with {NeedsText=false,NeedsPhoto=false},now).Code=="EMPTY_MESSAGE",
    "empty request blocked");
var grant=manager.TryReserve(j,proof,now);
Check(grant.Allowed&&grant.Permit is not null,"one permitted pinned send reservation");
Check(manager.TryReserve(j,proof,now).Code=="ACCOUNT_BUSY",
    "same account cannot acquire two in-flight permits");
Check(manager.TryReserve(Job(first,"-100002"),Proof(first,"-100002"),now).Code=="ACCOUNT_BUSY",
    "same account in different groups stays serialized");
Check(manager.TryReserve(Job(second),Proof(second),now).Code=="WAIT_PLATFORM_LIMIT",
    "chat-wide safety budget disallows alternating accounts immediately");
Check(!manager.Settle(new OutboundPermit(Guid.NewGuid(),j.RunId,j.Steps[0].StepKey,first,j.ChatId,null,now),true),
    "invalid token cannot unlock reserved account");
Check(manager.Settle(grant.Permit!,true),"definitive resolution releases exact reservation");
Check(!manager.Settle(grant.Permit!,true),"settled ticket cannot settle twice");
Check(manager.TryReserve(Job(first,"-100002"),Proof(first,"-100002"),now.AddSeconds(2)).Code=="WAIT_PLATFORM_LIMIT",
    "account cooldown applies to every group");
var secondJob=Job(second);
var later=manager.TryReserve(secondJob,Proof(second,authTime:now.AddSeconds(4),
    permissionTime:now.AddSeconds(4)),now.AddSeconds(4));
Check(later.Allowed,"another account may run after group cooldown and fresh permission");
Check(manager.TryReserve(j,Proof(first,authTime:now.AddSeconds(4),
    permissionTime:now.AddSeconds(4)),now.AddSeconds(4)).Code=="WAIT_PLATFORM_LIMIT",
    "same group cannot interleave alternate accounts during group cooldown");
Check(manager.Settle(later.Permit!,true),"another account released after definitive simulated result");

var slow=new OutboundCoordinator(TimeSpan.FromSeconds(1),TimeSpan.FromSeconds(1));
var slowJob=Job(first,"-100003");
var slowPermit=slow.TryReserve(slowJob,Proof(first,"-100003",slow:45),now);
Check(slowPermit.Allowed,"slow-mode group starts with first permitted reservation");
Check(slow.Settle(slowPermit.Permit!,true),"slow-mode first reservation settles");
Check(slow.TryReserve(slowJob,Proof(first,"-100003",authTime:now.AddSeconds(10),
    permissionTime:now.AddSeconds(10),slow:45),now.AddSeconds(10)).RetryAtUtc==now.AddSeconds(45),
    "45-second slow-mode deadline cannot be reduced by local settings");
Check(slow.TryReserve(Job(second,"-100003"),Proof(second,"-100003",
    authTime:now.AddSeconds(10),permissionTime:now.AddSeconds(10)),now.AddSeconds(10)).Allowed,
    "other legitimately authorized account independent of per-member slow mode when no chat-wide notice");
slow.ApplyPlatformLimit(first,"-100003",PlatformLimitScope.Chat,TimeSpan.FromSeconds(100),now.AddSeconds(10));
Check(slow.TryReserve(Job(third,"-100003"),Proof(third,"-100003",
    authTime:now.AddSeconds(15),permissionTime:now.AddSeconds(15)),now.AddSeconds(15)).RetryAtUtc==now.AddSeconds(110),
    "official chat-level SLOWMODE_WAIT applies to all accounts");
Check(slow.TryReserve(Job(first,"-100003"),Proof(first,"-100003",
    authTime:now.AddSeconds(50),permissionTime:now.AddSeconds(50)),now.AddSeconds(50)).Code=="WAIT_PLATFORM_LIMIT",
    "server chat wait overrides previous 45-second local deadline");
Reject(()=>slow.ApplyPlatformLimit(first,"-100003",PlatformLimitScope.Chat,TimeSpan.Zero,now),
    "zero official wait invalid");
Reject(()=>slow.ApplyPlatformLimit(first,"-100003",PlatformLimitScope.Account,TimeSpan.FromDays(8),now),
    "invalid extreme wait rejected");

var flood=new OutboundCoordinator();
flood.ApplyPlatformLimit(first,"-100004",PlatformLimitScope.Account,TimeSpan.FromSeconds(120),now);
Check(flood.TryReserve(Job(first,"-100099"),Proof(first,"-100099"),now.AddSeconds(20)).RetryAtUtc==now.AddSeconds(120),
    "FLOOD_WAIT blocks affected account even in other groups");
Check(flood.TryReserve(Job(second,"-100004"),Proof(second,"-100004"),now.AddSeconds(20)).Code=="WAIT_PLATFORM_LIMIT",
    "FLOOD_WAIT affected chat cannot be bypassed by account rotation");
Check(flood.TryReserve(Job(second,"-100005"),Proof(second,"-100005"),now.AddSeconds(20)).Allowed,
    "unaffected independently authorized chat retains eligibility");
flood.ApplyPlatformLimit(first,"-100004",PlatformLimitScope.Account,TimeSpan.FromSeconds(60),now.AddSeconds(10));
Check(flood.TryReserve(Job(first,"-100099"),Proof(first,"-100099"),now.AddSeconds(30)).RetryAtUtc==now.AddSeconds(120),
    "shorter subsequent rate notice cannot shorten existing deadline");

var unknown=new OutboundCoordinator();
var uncertain=unknown.TryReserve(Job(first),Proof(first),now);
Check(uncertain.Allowed,"unknown-outcome test obtains a single ticket");
Check(unknown.Settle(uncertain.Permit!,false),"unknown delivery explicitly marked unresolved");
Check(!unknown.Settle(uncertain.Permit!,true),"uncertain delivery cannot later be called definitely unsent");
Check(unknown.TryReserve(Job(first,"-100009"),Proof(first,"-100009"),
    now.AddSeconds(200)).Code=="OUTCOME_UNKNOWN",
    "account stays blocked after indeterminate delivery; no blind resend");

var limited=new OutboundCoordinator(TimeSpan.FromSeconds(1),TimeSpan.FromSeconds(1),6);
for(int i=0;i<6;i++)
{
    var t=now.AddSeconds(i*2);
    var acquired=limited.TryReserve(Job(first),Proof(first,authTime:t,permissionTime:t),t);
    Check(acquired.Allowed,"bounded 60-second account frequency slot "+i);
    Check(limited.Settle(acquired.Permit!,true),"bounded slot settled "+i);
}
var seventh=now.AddSeconds(12);
Check(limited.TryReserve(Job(first),Proof(first,authTime:seventh,permissionTime:seventh),seventh).RetryAtUtc==now.AddMinutes(1),
    "seventh attempted reservation waits until first of six slots ages out");
var minute=now.AddMinutes(1);
Check(limited.TryReserve(Job(first),Proof(first,authTime:minute,permissionTime:minute),minute).Allowed,
    "budget re-opens at exact 60-second boundary");
Reject(()=>new OutboundCoordinator(TimeSpan.Zero,TimeSpan.FromSeconds(3)),
    "account spacing floor enforced");
Reject(()=>new OutboundCoordinator(TimeSpan.FromSeconds(3),TimeSpan.Zero),
    "group spacing floor enforced");
Reject(()=>new OutboundCoordinator(maxAccountMinute:30),
    "account minute budget cannot be increased above safety bound");

var topics=new OutboundCoordinator();
var targetTopic=Job(first,topic:"12");
Check(topics.TryReserve(targetTopic,Proof(first,topic:"13"),now).Code=="BINDING_MISMATCH",
    "target topic cannot be silently switched");
var closed=Proof(first,topic:"12") with {Topic=new TopicInfo("-100001","12","Closed",true,false,now)};
Check(topics.TryReserve(targetTopic,closed,now).Code=="TOPIC_UNAVAILABLE","closed topic blocked");
Check(topics.TryReserve(targetTopic,Proof(first,topic:"12"),now).Allowed,
    "valid exact topic binding allowed for simulation");

var concurrent=new OutboundCoordinator();
var concurrentJob=Job(first);
int allowed=0;
Parallel.For(0,40,_=>{
    if(concurrent.TryReserve(concurrentJob,Proof(first),now).Allowed)
        Interlocked.Increment(ref allowed);
});
Check(allowed==1,"40 parallel requests to one account yield exactly one permit");
concurrent.Shutdown();
Check(concurrent.TryReserve(concurrentJob,Proof(first),now.AddDays(1)).Code=="SHUTDOWN",
    "shutdown closes all new account reservations");

var across=new OutboundCoordinator();
int fifty=0;
Parallel.For(0,50,i=>{
    var group="-100"+(5000+i).ToString();
    if(across.TryReserve(Job(first,group),Proof(first,group),now).Allowed)
        Interlocked.Increment(ref fifty);
});
Check(fifty==1,"50 concurrent group tasks sharing one account never dispatch in parallel");
Console.WriteLine($"SUMMARY: {count} outbound policy checks passed; no live accounts, TDLib or network messages.");
