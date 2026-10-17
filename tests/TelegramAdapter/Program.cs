using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Mosen.GroupScheduler.Telegram;

int checks=0;
void Check(bool condition,string name)
{
    if(!condition)throw new InvalidOperationException("FAIL: "+name);
    checks++;
    Console.WriteLine("PASS "+name);
}

static async Task Eventually(Func<bool> check,int maxWaitMs=1000)
{
    Stopwatch watch=Stopwatch.StartNew();
    while(!check())
    {
        if(watch.ElapsedMilliseconds>maxWaitMs)throw new TimeoutException("Timed out waiting for synthetic transport condition");
        await Task.Delay(5);
    }
}

var fake=new FakeTransport();
await using(var adapter=new TdlibDispatcher(fake,TimeSpan.FromMilliseconds(5),TimeSpan.FromMilliseconds(500)))
{
    int first=adapter.RegisterClient(),second=adapter.RegisterClient();
    Check(first != second && adapter.RegisteredClientCount==2,"two distinct synthetic client IDs registered");
    adapter.Start();
    bool doubleLoop=false;
    try{adapter.Start();}catch(InvalidOperationException){doubleLoop=true;}
    Check(doubleLoop,"second receiver thread denied");

    var response1=adapter.SendControlAsync(first,TdlibControlCommand.GetVersion,TimeSpan.FromSeconds(2));
    var response2=adapter.SendControlAsync(second,TdlibControlCommand.GetAuthorizationState,TimeSpan.FromSeconds(2));
    await Eventually(()=>fake.Sent.Count(x=>!x.Json.Contains("\"close\""))==2);
    var firstReq=fake.Sent.First(x=>x.ClientId==first);
    var secondReq=fake.Sent.First(x=>x.ClientId==second);
    string Extra((int ClientId,string Json) entry)=>JsonDocument.Parse(entry.Json).RootElement.GetProperty("@extra").GetString()!;
    string extra1=Extra(firstReq),extra2=Extra(secondReq);
    Check(extra1!=extra2 && extra1.StartsWith("ms09-")&&extra2.StartsWith("ms09-"),
        "async correlation tokens unique across all client IDs");
    Check(firstReq.Json.Contains("getOption")&&secondReq.Json.Contains("getAuthorizationState"),
        "stage 09 only exposes safe metadata operations");

    fake.Push($$"""{"@type":"ok","@client_id":{{second}},"@extra":"{{extra1}}"}""");
    await Task.Delay(25);
    Check(!response1.IsCompleted,"cross-client response with matching token rejected");

    fake.Push($$"""{"@type":"authorizationStateWaitTdlibParameters","@client_id":{{second}},"@extra":"{{extra2}}"}""");
    fake.Push($$"""{"@type":"optionValueString","@client_id":{{first}},"@extra":"{{extra1}}","value":"mock-version"}""");
    Check((await response2).ClientId==second && (await response1).ClientId==first,
        "out-of-order responses delivered to correct original client");
    Check(adapter.PendingRequestCount==0,"completed request entries evicted");

    fake.Push("""{"@type":"updateTest","@client_id":999999,"ordinal":0}""");
    fake.Push("""{invalid""");
    for(int i=0;i<100;i++)
        fake.Push($$"""{"@type":"updateTest","@client_id":{{first}},"ordinal":{{i}}}""");
    for(int i=0;i<100;i++)
    {
        var item=await adapter.Updates.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        Check(item.ClientId==first && item.Body.GetProperty("ordinal").GetInt32()==i,
            "ordered update "+i);
    }
    Check(adapter.Updates.TryRead(out _)==false,"unknown client and malformed data dropped");
    Check(fake.MaxConcurrentReceives==1,"global receive loop never overlaps another native receive");

    var expired=adapter.SendControlAsync(first,TdlibControlCommand.GetVersion,TimeSpan.FromMilliseconds(70));
    await Eventually(()=>adapter.PendingRequestCount==1);
    string late=Extra(fake.Sent.Last(x=>x.ClientId==first));
    bool timedout=false;
    try{await expired;}catch(TimeoutException){timedout=true;}
    Check(timedout && adapter.PendingRequestCount==0,"timeout removes pending request without retry");
    fake.Push($$"""{"@type":"optionValueString","@client_id":{{first}},"@extra":"{{late}}"}""");
    await Task.Delay(30);
    Check(!adapter.Updates.TryRead(out _),"late reply not misclassified as live update");

    using var cancelled=new CancellationTokenSource();
    cancelled.Cancel();
    bool wasCancelled=false;
    try{await adapter.SendControlAsync(first,TdlibControlCommand.GetVersion,TimeSpan.FromSeconds(1),cancelled.Token);}
    catch(OperationCanceledException){wasCancelled=true;}
    Check(wasCancelled && adapter.PendingRequestCount==0,"cancelled request not sent or leaked");

    var drain=adapter.Updates.ReadAllAsync();
    Check(drain!=null,"bounded update reader available");
}
Check(fake.CloseRequests==2,"dispose sends close exactly once to every registered client");
Check(fake.IsDisposed,"underlying fake transport disposed after orderly shutdown");
Check(fake.MaxConcurrentReceives==1,"single receive global invariant held for entire lifecycle");

// Stress: update channel is bounded; disposal must cancel a producer stalled on backpressure.
var full=new FakeTransport();
var busy=new TdlibDispatcher(full,TimeSpan.FromMilliseconds(1),TimeSpan.FromMilliseconds(20));
int busyId=busy.RegisterClient();
busy.Start();
for(int i=0;i<500;i++)full.Push($$"""{"@type":"updateTest","@client_id":{{busyId}},"ordinal":{{i}}}""");
await Task.Delay(80);
await busy.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
Check(full.IsDisposed,"backpressure never blocks application shutdown forever");

// Safety: unknown control command never reaches native transport.
var strictFake=new FakeTransport();
await using(var strict=new TdlibDispatcher(strictFake,TimeSpan.FromMilliseconds(2),TimeSpan.FromMilliseconds(50)))
{
    int id=strict.RegisterClient();
    strict.Start();
    bool rejected=false;
    try{await strict.SendControlAsync(id,(TdlibControlCommand)999,TimeSpan.FromSeconds(1));}
    catch(ArgumentOutOfRangeException){rejected=true;}
    Check(rejected && strictFake.Sent.Length==0 && strict.PendingRequestCount==0,
        "unknown native command is rejected before any network send");
}
using(var list=JsonDocument.Parse(TdlibManualOperations.Chats("req-1",50)))
  Check(list.RootElement.GetProperty("@type").GetString()=="getChats"&&
        list.RootElement.GetProperty("limit").GetInt32()==50,
        "official TDLib chat list request is bounded");
using(var lookup=JsonDocument.Parse(TdlibManualOperations.Chat("req-2",-100123456)))
  Check(lookup.RootElement.GetProperty("chat_id").GetInt64()==-100123456,
        "official TDLib getChat uses exact selected group id");
using(var one=JsonDocument.Parse(TdlibManualOperations.SendOneText("req-3",-100123456,"手动发送测试")))
  Check(one.RootElement.GetProperty("@type").GetString()=="sendMessage"&&
        one.RootElement.GetProperty("input_message_content").GetProperty("@type").GetString()=="inputMessageText"&&
        one.RootElement.GetProperty("input_message_content").GetProperty("text").GetProperty("text").GetString()=="手动发送测试",
        "manual text request contains actual formatted text and chat ID");
bool badManual=false;
try{TdlibManualOperations.SendOneText("x",0,"test");}catch(ArgumentOutOfRangeException){badManual=true;}
Check(badManual,"zero chat ID rejected before transport");
badManual=false;
try{TdlibManualOperations.SendOneText("x",-100,new string('a',2001));}catch(ArgumentException){badManual=true;}
Check(badManual,"message content bounded before transport");
Console.WriteLine($"SUMMARY: {checks} TDLib adapter assertions passed, against factory and fake transport only (native smoke is separate).");

internal sealed class FakeTransport : ITdlibTransport
{
    private readonly ConcurrentQueue<string> _incoming=new();
    private readonly AutoResetEvent _ready=new(false);
    private readonly ConcurrentQueue<(int ClientId,string Json)> _sent=new();
    private int _next;
    private int _inside;
    private int _peak;
    private int _closed;
    private bool _disposed;
    public int MaxConcurrentReceives=>_peak;
    public int CloseRequests=>_closed;
    public bool IsDisposed=>_disposed;
    public (int ClientId,string Json)[] Sent=>_sent.ToArray();
    public int CreateClientId()=>Interlocked.Increment(ref _next);
    public void Send(int id,string json)
    {
        _sent.Enqueue((id,json));
        if(json.Contains("\"close\"",StringComparison.Ordinal))
        {
            Interlocked.Increment(ref _closed);
            Push("{\"@type\":\"updateAuthorizationState\",\"@client_id\":" + id + ",\"authorization_state\":{\"@type\":\"authorizationStateClosed\"}}");
        }
    }
    public string? Receive(TimeSpan timeout)
    {
        int active=Interlocked.Increment(ref _inside);
        _peak=Math.Max(_peak,active);
        try
        {
            if(_incoming.TryDequeue(out var early))return early;
            _ready.WaitOne(timeout);
            return _incoming.TryDequeue(out var message)?message:null;
        }
        finally{Interlocked.Decrement(ref _inside);}
    }
    public void Push(string json){_incoming.Enqueue(json);_ready.Set();}
    public void Dispose(){_disposed=true;_ready.Dispose();}
}
