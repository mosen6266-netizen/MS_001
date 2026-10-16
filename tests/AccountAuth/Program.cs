using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Mosen.GroupScheduler.AccountAuth;
using Mosen.GroupScheduler.Telegram;

int count=0;
void Check(bool ok,string label)
{
    if(!ok)throw new Exception("FAIL: "+label);
    count++;Console.WriteLine("PASS "+label);
}
TdlibEnvelope Envelope(int id,string type,string extraState="") {
    var state=extraState.Length==0?new Dictionary<string,object?>{{"@type",type}}:
        new Dictionary<string,object?>{{"@type",type},{"link",extraState}};
    return new TdlibEnvelope(id,"updateAuthorizationState",JsonSerializer.SerializeToElement(new Dictionary<string,object?> {
        ["@type"]="updateAuthorizationState",["@client_id"]=id,["authorization_state"]=state
    }));
}
TdlibEnvelope Answer(int id,string type="ok",int? code=null) =>
    new(id,type,JsonSerializer.SerializeToElement(new Dictionary<string,object?>{
        ["@type"]=type,["@client_id"]=id,["code"]=code
    }));
var states = new (string Official,AuthPhase Phase,string Chinese)[]{
    ("authorizationStateWaitTdlibParameters",AuthPhase.WaitParameters,"配置"),
    ("authorizationStateWaitPhoneNumber",AuthPhase.WaitPhoneOrQr,"登录"),
    ("authorizationStateWaitOtherDeviceConfirmation",AuthPhase.WaitQrConfirmation,"确认"),
    ("authorizationStateWaitCode",AuthPhase.WaitCode,"验证码"),
    ("authorizationStateWaitEmailAddress",AuthPhase.WaitEmailAddress,"邮箱"),
    ("authorizationStateWaitEmailCode",AuthPhase.WaitEmailCode,"邮箱"),
    ("authorizationStateWaitPassword",AuthPhase.WaitPassword,"密码"),
    ("authorizationStateWaitRegistration",AuthPhase.WaitRegistration,"注册"),
    ("authorizationStateWaitPremiumPurchase",AuthPhase.WaitPremiumPurchase,"官方"),
    ("authorizationStateReady",AuthPhase.Ready,"成功"),
    ("authorizationStateLoggingOut",AuthPhase.LoggingOut,"退出"),
    ("authorizationStateClosing",AuthPhase.Closing,"关闭"),
    ("authorizationStateClosed",AuthPhase.Closed,"关闭")
};
foreach(var x in states)
{
    var v=AuthStateReader.FromUpdate(Envelope(42,x.Official),1);
    Check(v.Phase==x.Phase && v.Title.Contains(x.Chinese),
        "TDLib official state -> Chinese UI: "+x.Official);
}
Check(AuthStateReader.FromUpdate(Envelope(42,"authorizationStateFuture"),5).Phase==AuthPhase.Unsupported,
    "unknown future auth state is blocked with Chinese help");

var gateway=new FakeGateway(Answer);
var login=new AuthSession(42,gateway);
Check(!login.Accept(Envelope(99,"authorizationStateWaitPhoneNumber")),
    "cross-client update rejected");
Check(login.Accept(Envelope(42,"authorizationStateWaitTdlibParameters")),
    "real initialization state update accepted");
Check((await login.SubmitAsync(new(AuthCommandKind.Configure))).Accepted,
    "parameters submitted, without falsely becoming authorized");
Check(login.Snapshot.Phase==AuthPhase.WaitParameters,"request success does not imply Telegram Ready");
Check(login.Accept(Envelope(42,"authorizationStateWaitPhoneNumber")),
    "official phone / QR choice state received");
Check(!login.Accept(Envelope(42,"authorizationStateWaitPhoneNumber")),"duplicate auth state ignored");
Check(!(await login.SubmitAsync(new(AuthCommandKind.EnterCode,"121212"))).Accepted,
    "wrong-state code is refused before transport");
Check(!(await login.SubmitAsync(new(AuthCommandKind.EnterPhone,"invalid-phone"))).Accepted,
    "invalid international phone rejected before transport");
Check((await login.SubmitAsync(new(AuthCommandKind.StartQr))).Accepted,
    "QR request submitted once");
Check(!(await login.SubmitAsync(new(AuthCommandKind.StartQr))).Accepted,
    "same auth state cannot submit duplicate QR request");
var qrLink="tg://login?token=SECRET_QR_LINK_SHOULD_NOT_LOG";
Check(login.Accept(Envelope(42,"authorizationStateWaitOtherDeviceConfirmation",qrLink)),
    "QR link delivered from official auth state");
Check(login.Snapshot.QrLink==qrLink,"QR token available to volatile UI only");
Check(!login.Snapshot.ToString().Contains(qrLink),"QR token excluded from object diagnostics");
Check(login.Accept(Envelope(42,"authorizationStateWaitOtherDeviceConfirmation",qrLink+"-rotated")),
    "updated QR link replaces previous without allocating account");
Check((await login.SubmitAsync(new(AuthCommandKind.RefreshQr))).Accepted,
    "QR refresh is status query rather than a second QR auth");
Check(gateway.Commands.Count(x=>x.Kind==AuthCommandKind.StartQr)==1 &&
    gateway.Commands.Count(x=>x.Kind==AuthCommandKind.RefreshQr)==1,
    "no duplicate QR sessions or clients created");
Check(login.Accept(Envelope(42,"authorizationStateWaitCode")),"official code requirement");
Check((await login.SubmitAsync(new(AuthCommandKind.EnterCode,"123456"))).Accepted,"login code submitted");
Check(login.Snapshot.Phase==AuthPhase.WaitCode,"code acceptance not optimistic Ready");
Check(login.Accept(Envelope(42,"authorizationStateWaitEmailAddress")),"email address step");
Check((await login.SubmitAsync(new(AuthCommandKind.EnterEmailAddress,"owner@example.com"))).Accepted,"email address submitted");
Check(login.Accept(Envelope(42,"authorizationStateWaitEmailCode")),"email code step");
Check((await login.SubmitAsync(new(AuthCommandKind.EnterEmailCode,"ABC123"))).Accepted,"email code submitted");
Check(login.Accept(Envelope(42,"authorizationStateWaitPassword")),"2FA password step");
const string password="VERY_PRIVATE_2FA_VALUE";
Check((await login.SubmitAsync(new(AuthCommandKind.EnterPassword,password))).Accepted,"2FA submitted with typed command");
Check(!new AuthCommand(AuthCommandKind.EnterPassword,password).ToString().Contains(password),
    "auth command debug string always redacts secret");
Check(login.Accept(Envelope(42,"authorizationStateWaitRegistration")),"registration step");
Check(!(await login.SubmitAsync(new(AuthCommandKind.Register,"Example","",false))).Accepted,
    "terms of service must be explicitly accepted");
Check((await login.SubmitAsync(new(AuthCommandKind.Register,"Example","",true))).Accepted,
    "registration after explicit consent accepted");
Check(login.Accept(Envelope(42,"authorizationStateWaitPremiumPurchase")),"Telegram Premium purchase block shown");
Check(!(await login.SubmitAsync(new(AuthCommandKind.EnterCode,"123456"))).Accepted,
    "Premium/unsupported gate never bypassed");
Check(login.Accept(Envelope(42,"authorizationStateReady")),"Ready requires official state update");
Check(login.Snapshot.Phase==AuthPhase.Ready,"authorized state is official only");
Check((await login.SubmitAsync(new(AuthCommandKind.Cancel))).Accepted,"user can close a Ready client without invoking logout");
Check(gateway.CloseCount==1,"Cancel closes the same client exactly once");
Check(!(await login.SubmitAsync(new(AuthCommandKind.Cancel))).Accepted,"repeated cancel does not create a duplicate close");
Check(!login.Accept(Envelope(42,"authorizationStateReady")),"late Ready cannot resurrect cancelled client");
Check(login.Accept(Envelope(42,"authorizationStateClosed")),"official Closed acknowledged after cancellation");
Check(login.Snapshot.Phase==AuthPhase.Closed,"closed state presented");

var rejectedGateway=new FakeGateway((id,cmd)=>Answer(id,"error",429));
var restricted=new AuthSession(43,rejectedGateway);
restricted.Accept(Envelope(43,"authorizationStateWaitCode"));
var tooFast=await restricted.SubmitAsync(new(AuthCommandKind.EnterCode,"55555"));
Check(!tooFast.Accepted && tooFast.Code=="RATE_LIMIT","Telegram 429 blocks retry and suggests official wait");
Check(restricted.Snapshot.Phase==AuthPhase.WaitCode,"rate limit does not fake Ready");
rejectedGateway.Response=(id,cmd)=>Answer(id,"error",400);
var wrong=await restricted.SubmitAsync(new(AuthCommandKind.EnterCode,"55555"));
Check(!wrong.Accepted&&wrong.Code=="REJECTED","invalid code cannot leak private Telegram error details");

var pendingGateway=new FakeGateway(Answer){Delay=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)};
var ongoing=new AuthSession(44,pendingGateway);
ongoing.Accept(Envelope(44,"authorizationStateWaitCode"));
var first=ongoing.SubmitAsync(new(AuthCommandKind.EnterCode,"123456"));
var repeats=await Task.WhenAll(Enumerable.Range(0,500).Select(_=>ongoing.SubmitAsync(new(AuthCommandKind.EnterCode,"123456"))));
Check(repeats.All(x=>!x.Accepted && x.Code=="BUSY") &&
    pendingGateway.Commands.Count==1,"500 concurrent submits only send one auth request");
var cancel=await ongoing.SubmitAsync(new(AuthCommandKind.Cancel));
Check(cancel.Accepted&&pendingGateway.CloseCount==1,
    "Cancel immediately closes client even while previous request awaits response");
pendingGateway.Delay.SetResult(true);
var late=await first;
Check(!late.Accepted && late.Code=="CLOSED","pending answer cannot revive cancelled auth");
Check(!ongoing.Accept(Envelope(44,"authorizationStateReady")),"late Ready update ignored after cancel");

var unknown=new AuthSession(45,new FakeGateway((id,cmd)=>throw new TimeoutException("PRIVATE_PHONE=+12025550001")));
unknown.Accept(Envelope(45,"authorizationStateWaitPassword"));
var timeout=await unknown.SubmitAsync(new(AuthCommandKind.EnterPassword,password));
Check(timeout.Code=="UNKNOWN_OUTCOME"&&!timeout.Message.Contains(password),"unknown outcome is sanitized and never resent");

var fakeNative=new FakeTdlibTransport();
await using(var dispatcher=new TdlibDispatcher(fakeNative,TimeSpan.FromMilliseconds(5),TimeSpan.FromMilliseconds(250)))
{
    int client=dispatcher.RegisterClient();
    dispatcher.Start();
    var config=new TdlibAuthConfiguration(12345,"0123456789abcdef0123456789abcdef",
        Path.Combine(Path.GetTempPath(),"ms001-auth-fake-db"),Path.Combine(Path.GetTempPath(),"ms001-auth-fake-files"),
        new byte[32],true);
    var bridge=new TdlibAuthGateway(dispatcher,config);
    var commands=new[]{
        new AuthCommand(AuthCommandKind.Configure),
        new AuthCommand(AuthCommandKind.StartQr),
        new AuthCommand(AuthCommandKind.RefreshQr),
        new AuthCommand(AuthCommandKind.EnterPhone,"+12025550199"),
        new AuthCommand(AuthCommandKind.EnterCode,"63825"),
        new AuthCommand(AuthCommandKind.EnterEmailAddress,"mail@example.org"),
        new AuthCommand(AuthCommandKind.EnterEmailCode,"EMAIL001"),
        new AuthCommand(AuthCommandKind.EnterPassword,password),
        new AuthCommand(AuthCommandKind.Register,"Example","Last",true)
    };
    string[] names={"setTdlibParameters","requestQrCodeAuthentication","getAuthorizationState",
        "setAuthenticationPhoneNumber","checkAuthenticationCode","setAuthenticationEmailAddress",
        "checkAuthenticationEmailCode","checkAuthenticationPassword","registerUser"};
    for(int i=0;i<commands.Length;i++)
    {
        var received=await bridge.ExecuteAsync(client,commands[i],TimeSpan.FromSeconds(2),CancellationToken.None);
        Check(received.Type=="ok","TDLib correlated reply for "+names[i]);
    }
    var sent=fakeNative.Sent.Where(x=>!x.Contains("\"close\"")).ToArray();
    Check(sent.Length==names.Length,"9 allowlisted TDLib authentication wire requests only");
    for(int i=0;i<sent.Length;i++)
    {
        using var doc=JsonDocument.Parse(sent[i]);
        Check(doc.RootElement.GetProperty("@type").GetString()==names[i],"official TDLib wire kind "+names[i]);
        Check(doc.RootElement.TryGetProperty("@extra",out _),"wire request includes correlation");
    }
    using var last=JsonDocument.Parse(sent.Single(s=>s.Contains("\"setTdlibParameters\"")));
    Check(last.RootElement.GetProperty("database_encryption_key").ValueKind==JsonValueKind.String,
        "database encryption key encoded as TDLib JSON bytes");
    using var codeWire=JsonDocument.Parse(sent.Single(s=>s.Contains("\"checkAuthenticationEmailCode\"")));
    Check(codeWire.RootElement.GetProperty("code").GetProperty("@type").GetString()=="emailAddressAuthenticationCode",
        "email code uses official nested type");
    await bridge.CloseAsync(client,CancellationToken.None);
    Check(fakeNative.CloseCount>=1,"cancel uses TDLib close not logout");
}
Check(fakeNative.IsDisposed,"auth transport disposed on dispatcher shutdown");
Console.WriteLine("SUMMARY: "+count+" authorization state and wire assertions passed (synthetic only).");

internal sealed class FakeGateway : IAuthGateway
{
    public readonly ConcurrentQueue<AuthCommand> Commands=new();
    public int CloseCount;
    public Func<int,AuthCommand,TdlibEnvelope> Response;
    public TaskCompletionSource<bool>? Delay;
    public FakeGateway(Func<int,string,int?,TdlibEnvelope> answer)
    {
        Response=(id,cmd)=>answer(id,"ok",null);
    }
    public FakeGateway(Func<int,AuthCommand,TdlibEnvelope> answer){Response=answer;}
    public async Task<TdlibEnvelope> ExecuteAsync(int id,AuthCommand command,TimeSpan timeout,CancellationToken ct)
    {
        Commands.Enqueue(command);
        if(Delay!=null)await Delay.Task.WaitAsync(ct);
        return Response(id,command);
    }
    public Task CloseAsync(int id,CancellationToken ct){Interlocked.Increment(ref CloseCount);return Task.CompletedTask;}
}
internal sealed class FakeTdlibTransport : ITdlibTransport
{
    private readonly ConcurrentQueue<string> _responses=new();
    private readonly AutoResetEvent _event=new(false);
    private readonly List<string> _sent=new();
    private int _next;
    private bool _disposed;
    public string[] Sent{get{lock(_sent)return _sent.ToArray();}}
    public int CloseCount;
    public bool IsDisposed=>_disposed;
    public int CreateClientId()=>Interlocked.Increment(ref _next);
    public void Send(int id,string request)
    {
        lock(_sent)_sent.Add(request);
        using var parsed=JsonDocument.Parse(request);
        if(parsed.RootElement.GetProperty("@type").GetString()=="close")
        {
            Interlocked.Increment(ref CloseCount);
            _responses.Enqueue(JsonSerializer.Serialize(new Dictionary<string,object?>{
                ["@type"]="updateAuthorizationState",["@client_id"]=id,
                ["authorization_state"]=new Dictionary<string,object?>{["@type"]="authorizationStateClosed"}
            }));
        }
        else
        {
            string correlation=parsed.RootElement.GetProperty("@extra").GetString()!;
            _responses.Enqueue(JsonSerializer.Serialize(new Dictionary<string,object?>{
                ["@type"]="ok",["@client_id"]=id,["@extra"]=correlation
            }));
        }
        _event.Set();
    }
    public string? Receive(TimeSpan wait)
    {
        if(_responses.TryDequeue(out var s))return s;
        _event.WaitOne(wait);
        return _responses.TryDequeue(out var message)?message:null;
    }
    public void Dispose(){_disposed=true;_event.Dispose();}
}
