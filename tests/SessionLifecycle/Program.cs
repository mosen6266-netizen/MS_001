using System.Collections.Concurrent;
using System.Security.Cryptography;
using Mosen.GroupScheduler.Persistence;
using Mosen.GroupScheduler.Core.Sessions;
using Mosen.GroupScheduler.AccountAuth;

int checks=0;
void Check(bool result,string title)
{
    if(!result)throw new Exception("FAIL: "+title);
    checks++;
    Console.WriteLine("PASS "+title);
}
var root=Path.Combine(Path.GetTempPath(),"ms001-step11-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    string metadata=Path.Combine(root,"app.db");
    var reg=new AccountRegistry(new AppDatabase(metadata));
    var entries=new List<AccountEntry>();
    for(int i=1;i<=300;i++)
        entries.Add(reg.Register("测试账号"+i.ToString("D3"),i<=150?"业务组A":"业务组B"));
    Check(entries.Count==300,"300 synthetic accounts registered");
    Check(entries.Select(x=>x.Id).Distinct().Count()==300,"all local identifiers unique");
    Check(entries.All(x=>x.CanAttachSession),"all new IDs are GUID session-capable");
    Check(reg.Find(pageSize:20).Total==300,"registry returns 300 metadata rows");
    Check(reg.Find(pageSize:20).Items.Count==20,"page size bounded to 20");
    Check(reg.Find(page:6,pageSize:50).Items.Count==50,"page 6 of 50 completes 300 records");
    Check(reg.Find(query:"测试账号150",pageSize:10).Total==1,"Chinese alias search matches exact record");
    Check(reg.Find(group:"业务组A",pageSize:20).Total==150,"group A isolation");
    Check(reg.Find(group:"业务组B",pageSize:20).Total==150,"group B isolation");
    bool full=false;
    try{reg.Register("第301个");}catch(InvalidOperationException){full=true;}
    Check(full,"301st account rejected without allocating session");
    var edited=reg.Edit(Guid.Parse(entries[0].Id),1,"团队主管","管理");
    Check(edited.Alias=="团队主管"&&edited.GroupTag=="管理"&&edited.Revision==2,"alias and group changed atomically");
    bool stale=false;
    try{reg.Edit(Guid.Parse(entries[0].Id),1,"旧版覆盖","旧组");}catch(AccountVersionConflict){stale=true;}
    Check(stale&&reg.Get(Guid.Parse(entries[0].Id))!.Alias=="团队主管","old revision refused without modifying remark");
    var verified=reg.BindVerifiedIdentity(Guid.Parse(edited.Id),edited.Revision,123456789,"Test User");
    Check(verified.VerifiedUserId=="123456789","verified Telegram identity stored as identifier only");
    bool duplicate=false;
    try{reg.BindVerifiedIdentity(Guid.Parse(entries[1].Id),1,123456789,"Copy");}
    catch(InvalidOperationException){duplicate=true;}
    Check(duplicate,"verified identity unique across 300 account slots");
    bool confirmation=false;
    try{reg.Archive(Guid.Parse(verified.Id),verified.Revision,"wrong remark");}
    catch(InvalidOperationException){confirmation=true;}
    Check(confirmation&&reg.Find().Total==300,"wrong archive confirmation preserves account");
    reg.Archive(Guid.Parse(verified.Id),verified.Revision,verified.Alias);
    Check(reg.Find().Total==299&&reg.Get(Guid.Parse(verified.Id))==null,"confirmed archive hides account without deleting files");
    var deleted=reg.Get(Guid.Parse(verified.Id),includeRemoved:true)!;
    Check(deleted.Removed&&deleted.Revision==verified.Revision+1,"archived metadata retained with revision");
    reg.Restore(Guid.Parse(deleted.Id),deleted.Revision);
    Check(reg.Find().Total==300&&reg.Get(Guid.Parse(deleted.Id))!.Alias=="团队主管","archive reversible and alias retained");
    var restarted=new AccountRegistry(new AppDatabase(metadata));
    Check(restarted.Find().Total==300,"300 metadata records persist across restart");
    var copy=new AppDatabase(metadata).CreateBackup();
    Check(File.Exists(copy),"300-row registry supports online backup");
    Check(restarted.Find(pageSize:10).Items.Count==10,"bounded pagination persists after restart");

    // Vault: on Windows use actual DPAPI CurrentUser. Linux uses test-only reversible
    // envelope for synthetic tests; it is not shipped as production cryptography.
    var prot=OperatingSystem.IsWindows()?(IKeyEnvelopeProtector)new WindowsUserKeyProtector():new SyntheticOnlyProtector();
    string secretRoot=Path.Combine(root,"tdlib-sessions");
    var vault=new SessionVault(secretRoot,prot);
    Guid[] identities=entries.Take(18).Select(e=>Guid.Parse(e.Id)).ToArray();
    foreach(Guid id in identities)vault.InitializeNew(id);
    Check(identities.All(vault.Exists),"18 synthetic account keys created in isolated directories");
    Check(Directory.GetDirectories(secretRoot).Length==18,"one directory per account, no shared session DB");
    var keyPath=Path.Combine(secretRoot,identities[0].ToString("D"),"db-key.dpapi");
    byte[] before;
    using(var lease=vault.OpenKey(identities[0]))before=lease.Bytes.ToArray();
    vault.InitializeNew(identities[0]);
    using(var after=vault.OpenKey(identities[0]))
    {
        Check(CryptographicOperations.FixedTimeEquals(before,after.Bytes),"reinitialization retains original session key");
    }
    CryptographicOperations.ZeroMemory(before);
    vault.EnsureDirectories(identities[0]);
    var directory=vault.Paths(identities[0]);
    Check(Directory.Exists(directory.DatabaseDirectory)&&Directory.Exists(directory.FilesDirectory),"TDLib DB and media paths isolated");
    Check(!File.ReadAllText(keyPath).Contains("TEST-ONLY-EPHEMERAL-SECRET"),"key envelope contains no raw test label");
    bool missing=false;
    try{vault.OpenKey(Guid.NewGuid());}catch(FileNotFoundException){missing=true;}
    Check(missing,"missing key never regenerated implicitly on open");

    var runtime=new SyntheticRuntime();
    await using(var sup=new SessionSupervisor(vault,runtime,maxActiveConnections:4))
    {
        foreach(var entry in entries)sup.Register(Guid.Parse(entry.Id));
        Check(sup.RegisteredCount==300,"supervisor holds 300 slot IDs with no active TDLib clients");
        Check(sup.ConfirmedReady==0&&sup.OccupiedSlots==0,"registration never means online");
        bool over=false;try{sup.Register(Guid.NewGuid());}catch(InvalidOperationException){over=true;}
        Check(over,"301st supervisor registration refused");
        var activeIds=identities.Take(4).ToArray();
        await Task.WhenAll(activeIds.Select(id=>sup.OpenAsync(id)));
        Check(sup.OccupiedSlots==4&&sup.ConfirmedReady==0,"four connections awaiting authorization, none falsely Ready");
        using var timeout=new CancellationTokenSource(TimeSpan.FromMilliseconds(80));
        bool waitCancelled=false;
        try{await sup.OpenAsync(identities[4],timeout.Token);}catch(OperationCanceledException){waitCancelled=true;}
        Check(waitCancelled&&sup.OccupiedSlots==4,"fifth connection waits for bounded active slot");
        Check(sup.Get(identities[4]).Phase==SessionPhase.Dormant,"cancelled queue slot safely restored to dormant");
        Check(sup.ApplyOfficialAuth(activeIds[0],new AuthView(AuthPhase.Ready,"授权成功","来自模拟正式状态",false,false,false,true,null,2)),
            "official auth state transition accepted");
        Check(sup.ConfirmedReady==1,"only official Ready becomes online");
        await sup.CloseAsync(activeIds[0]);
        Check(sup.OccupiedSlots==3&&sup.ConfirmedReady==0,"confirmed native close releases one connection permit");
        await sup.OpenAsync(identities[4]);
        Check(sup.OccupiedSlots==4,"freed connection slot reused on demand");
        bool cannotArchive=false;
        try{sup.Archive(activeIds[1]);}catch(InvalidOperationException){cannotArchive=true;}
        Check(cannotArchive,"active account cannot be archived until confirmed closed");
        await sup.CloseAsync(activeIds[1]);
        sup.Archive(activeIds[1]);
        Check(sup.Get(activeIds[1]).Phase==SessionPhase.Archived,"offline account archived in resource pool");
        sup.Restore(activeIds[1]);
        Check(sup.Get(activeIds[1]).Phase==SessionPhase.Dormant,"restore requires reauthorization, not Ready");
        runtime.FailCloseFor=activeIds[2];
        bool uncertain=false;
        try{await sup.CloseAsync(activeIds[2]);}catch(IOException){uncertain=true;}
        Check(uncertain&&sup.Get(activeIds[2]).Phase==SessionPhase.NeedsReview,
            "uncertain close preserves slot and exposes NeedsReview");
        Check(sup.OccupiedSlots==3,"uncertain native close never frees slot by guessing");
        runtime.FailCloseFor=null;
        await sup.CloseAsync(activeIds[2]);
        Check(sup.Get(activeIds[2]).Phase==SessionPhase.Dormant,"manual confirmed close recovers held slot");
    }
    Check(runtime.OpenCount>=5,"synthetic active resources opened on demand only");
    Check(runtime.OpenCount<300,"300 registered accounts do not force 300 TDLib connections");
    Check(runtime.OpenIds.Distinct().Count()==runtime.OpenIds.Count,"no duplicate client opening in simulated test");
    Console.WriteLine($"SUMMARY: {checks} session lifecycle assertions passed. Registered 300 metadata only, no native Telegram login or message send.");
}
finally
{
    try{Directory.Delete(root,true);}catch(IOException){ }
}
internal sealed class SyntheticOnlyProtector:IKeyEnvelopeProtector
{
    public string Protect(ReadOnlySpan<byte> key)=>"SYNTHETIC-NOT-ENCRYPTED:"+Convert.ToBase64String(key);
    public byte[] Unprotect(string value)
    {
        if(!value.StartsWith("SYNTHETIC-NOT-ENCRYPTED:"))throw new CryptographicException();
        return Convert.FromBase64String(value["SYNTHETIC-NOT-ENCRYPTED:".Length..]);
    }
}
internal sealed class SyntheticRuntime:IAccountRuntime
{
    private readonly ConcurrentDictionary<Guid,bool> _open=new();
    public Guid? FailCloseFor;
    public int OpenCount;
    public List<Guid> OpenIds=new();
    public Task OpenAsync(Guid id,SessionPaths paths,SessionKeyLease key,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(key.Bytes.Length!=32||!Directory.Exists(paths.DatabaseDirectory))throw new InvalidOperationException();
        if(!_open.TryAdd(id,true))throw new InvalidOperationException("Duplicate open");
        lock(OpenIds)OpenIds.Add(id);
        Interlocked.Increment(ref OpenCount);
        return Task.CompletedTask;
    }
    public Task CloseAsync(Guid id,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(FailCloseFor==id)throw new IOException("Synthetic uncertain close");
        _open.TryRemove(id,out _);
        return Task.CompletedTask;
    }
}
