using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mosen.GroupScheduler.AccountAuth;
using Mosen.GroupScheduler.Core.Security;
using Mosen.GroupScheduler.Installation;
using Mosen.GroupScheduler.Telegram;

namespace Mosen.GroupScheduler.Desktop;

public partial class MainWindow
{
    private readonly SemaphoreSlim _liveMutex = new(1,1);
    private TdlibDispatcher? _liveTransport;
    private AuthSession? _liveAuth;
    private Task? _liveUpdates;
    private int _liveClient;
    private string _lastDelivery="尚未发送";
    private DateTimeOffset _nextManualSend=DateTimeOffset.MinValue;
    private List<LiveGroup> _liveGroups=[];
    private bool _liveClosing;
    private TaskCompletionSource<bool>? _acknowledgement;
    private long _pendingMessageId;
    private long _pendingChatId;
    private string? _nativeIssue;

    private sealed record LiveGroup(string ChatId,string Title,bool CanSend);
    private object LiveStatus()
    {
        var auth=_liveAuth?.Snapshot;
        return new {
            nativeReady=_liveTransport is not null,
            connected=auth?.Phase==AuthPhase.Ready,
            phase=(auth?.Phase??AuthPhase.Initializing).ToString(),
            title=auth?.Title??"尚未连接",
            instruction=auth?.Instruction??"请先在设置中保存应用API参数，然后连接Telegram。",
            qrLink=auth?.QrLink,
            canStartQr=auth?.CanStartQr??false,
            canSubmit=auth?.CanSubmit??false,
            canCancel=auth?.CanCancel??false,
            groups=_liveGroups.Select(x=>new{chatId=x.ChatId,title=x.Title,canSend=x.CanSend}).ToArray(),
            delivery=_lastDelivery,
            issue=_nativeIssue,
            outboundPending=_acknowledgement is not null
        };
    }

    private async Task HandleLiveTelegramAsync(string raw,string requestId,string type)
    {
        try
        {
            using var document=JsonDocument.Parse(raw);
            JsonElement payload=document.RootElement.TryGetProperty("payload",out var v)?v:default;
            object result;
            switch(type)
            {
                case "telegram.auth.status": result=LiveStatus();break;
                case "telegram.auth.start":
                    result=await LiveStartAsync();break;
                case "telegram.auth.submit":
                    result=await LiveAuthSubmitAsync(payload);break;
                case "telegram.auth.stop":
                    await LiveStopAsync();result=LiveStatus();break;
                case "telegram.chats.refresh":
                    result=await LiveRefreshChatsAsync();break;
                case "telegram.message.send":
                    result=await LiveSendTextAsync(payload);break;
                default: throw new ArgumentException("Unsupported Telegram operation.");
            }
            SendSettingResponse(requestId,type+".result",true,result);
        }
        catch(Exception e) when(e is InvalidOperationException or ArgumentException or IOException
            or UnauthorizedAccessException or DllNotFoundException or EntryPointNotFoundException
            or BadImageFormatException or TimeoutException or OperationCanceledException)
        {
            // Never expose phone number, QR link, Telegram raw JSON, API credentials or native errors.
            SendSettingResponse(requestId,"telegram.live.error",false,new {
                code="TELEGRAM_OPERATION_NOT_CONFIRMED",
                message=e is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException
                  ? "TDLib原生组件无法加载；请安装完整Windows x64版本并检查运行库。"
                  : "操作未确认。请查看官方Telegram状态后再决定是否重试；本程序不会自动补发。"
            });
        }
        catch(Exception)
        {
            SendSettingResponse(requestId,"telegram.live.error",false,new {
                code="TELEGRAM_UNAVAILABLE",message="Telegram连接暂不可用，操作未确认。"
            });
        }
    }

    private byte[] ReadOrCreateSessionKey()
    {
        string folder=Path.Combine(UserDataPaths.ForCurrentUser(),"tdlib","primary");
        Directory.CreateDirectory(folder);
        string keyPath=Path.Combine(folder,"key.dpapi");
        if(File.Exists(keyPath))
        {
            var key=DpapiProtector.Unprotect(File.ReadAllText(keyPath));
            if(key.Length!=32){CryptographicOperations.ZeroMemory(key);throw new InvalidDataException("Session key length.");}
            return key;
        }
        byte[] generated=RandomNumberGenerator.GetBytes(32);
        try
        {
            var envelope=DpapiProtector.Protect(generated);
            using var file=new FileStream(keyPath,FileMode.CreateNew,FileAccess.Write,FileShare.None);
            using var writer=new StreamWriter(file);
            writer.Write(envelope);
            return generated.ToArray();
        }
        finally{CryptographicOperations.ZeroMemory(generated);}
    }

    private async Task<object> LiveStartAsync()
    {
        await _liveMutex.WaitAsync();
        try
        {
            if(_liveTransport is not null)return LiveStatus();
            if(!_telegramApiConfig.Configured)
                throw new InvalidOperationException("Telegram API application credentials are not configured.");
            var native=new NativeTdlibTransport();
            var bridge=new TdlibDispatcher(native);
            try
            {
                int id=bridge.RegisterClient();
                bridge.Start();
                var credentials=_telegramApiConfig.Read();
                byte[] key=ReadOrCreateSessionKey();
                TdlibAuthConfiguration config;
                try
                {
                    string path=Path.Combine(UserDataPaths.ForCurrentUser(),"tdlib","primary");
                    string db=Path.Combine(path,"database");
                    string files=Path.Combine(path,"files");
                    Directory.CreateDirectory(db);Directory.CreateDirectory(files);
                    config=new TdlibAuthConfiguration(credentials.ApiId,credentials.ApiHash,db,files,key);
                }
                finally{CryptographicOperations.ZeroMemory(key);}
                _liveClient=id;
                _liveAuth=new AuthSession(id,new TdlibAuthGateway(bridge,config));
                _liveTransport=bridge;
                _liveClosing=false;_liveGroups.Clear();_nativeIssue=null;
                _liveUpdates=Task.Run(()=>ConsumeLiveUpdatesAsync(bridge));
                // The TDLib control result represents an authoritative state, not a fake Ready.
                TdlibEnvelope result=await bridge.SendControlAsync(id,TdlibControlCommand.GetAuthorizationState,
                    TimeSpan.FromSeconds(20));
                await ObserveAuthenticationAsync(result);
                return LiveStatus();
            }
            catch
            {
                _liveClosing=true;
                await bridge.DisposeAsync();
                _liveTransport=null;_liveAuth=null;_liveClient=0;
                throw;
            }
        }
        finally{_liveMutex.Release();}
    }

    private async Task ObserveAuthenticationAsync(TdlibEnvelope response)
    {
        if(_liveClosing || _liveAuth is null)return;
        TdlibEnvelope translated=response;
        if(response.Type.StartsWith("authorizationState",StringComparison.Ordinal))
        {
            translated=new TdlibEnvelope(response.ClientId,"updateAuthorizationState",
                JsonSerializer.SerializeToElement(new Dictionary<string,object> {
                    ["@type"]="updateAuthorizationState",["authorization_state"]=response.Body
                }));
        }
        if(translated.Type!="updateAuthorizationState")return;
        if(!_liveAuth.Accept(translated))return;
        var view=_liveAuth.Snapshot;
        if(view.Phase==AuthPhase.WaitParameters)
        {
            // User has provided API credentials; configure once when official TDLib requests it.
            var result=await _liveAuth.SubmitAsync(new AuthCommand(AuthCommandKind.Configure));
            if(!result.Accepted)_nativeIssue=result.Code;
        }
        if(view.Phase==AuthPhase.Ready)
            _nativeIssue=null;
    }

    private async Task ConsumeLiveUpdatesAsync(TdlibDispatcher bridge)
    {
        try
        {
            await foreach(var evt in bridge.Updates.ReadAllAsync())
            {
                if(_liveClosing)break;
                if(evt.Type=="updateAuthorizationState")
                {
                    await ObserveAuthenticationAsync(evt);
                    if(_liveAuth?.Snapshot.Phase!=AuthPhase.Ready)_liveGroups.Clear();
                }
                else if(evt.Type is "updateMessageSendSucceeded" or "updateMessageSendFailed")
                {
                    var body=evt.Body;
                    long original=body.TryGetProperty("old_message_id",out var oldId)&&oldId.TryGetInt64(out var n)?n:0;
                    long chat=0;
                    if(body.TryGetProperty("message",out var msg)&&msg.TryGetProperty("chat_id",out var c))
                        c.TryGetInt64(out chat);
                    if(original!=0&&original==_pendingMessageId&&(chat==0||chat==_pendingChatId))
                    {
                        bool accepted=evt.Type=="updateMessageSendSucceeded";
                        _lastDelivery=accepted?"Telegram确认已发出":"Telegram返回发送失败";
                        _acknowledgement?.TrySetResult(accepted);
                    }
                }
            }
        }
        catch(Exception)
        {
            _nativeIssue="RECEIVER_STOPPED";
            _lastDelivery="连接中断，发送结果需人工核对";
        }
    }

    private async Task<object> LiveAuthSubmitAsync(JsonElement payload)
    {
        var session=_liveAuth??throw new InvalidOperationException("No live Telegram login session.");
        string action=payload.GetProperty("action").GetString()!;
        var kind=action switch {
            "qr"=>AuthCommandKind.StartQr,
            "phone"=>AuthCommandKind.EnterPhone,
            "code"=>AuthCommandKind.EnterCode,
            "email"=>AuthCommandKind.EnterEmailAddress,
            "emailCode"=>AuthCommandKind.EnterEmailCode,
            "password"=>AuthCommandKind.EnterPassword,
            _=>throw new ArgumentException("Unknown auth action.")
        };
        string? input=action=="qr"?null:(payload.TryGetProperty("input",out var p)?p.GetString():null);
        AuthResult result=await session.SubmitAsync(new AuthCommand(kind,input));
        return new{accepted=result.Accepted,code=result.Code,message=result.Message,status=LiveStatus()};
    }

    private async Task<object> LiveRefreshChatsAsync()
    {
        var bridge=_liveTransport??throw new InvalidOperationException("Not connected.");
        if(_liveAuth?.Snapshot.Phase!=AuthPhase.Ready)throw new InvalidOperationException("Not authorized.");
        var list=await bridge.SendManualAsync(_liveClient,TdlibManualOperation.ListChats,0,null,TimeSpan.FromSeconds(20));
        if(list.Type!="chats"||!list.Body.TryGetProperty("chat_ids",out var ids)||ids.ValueKind!=JsonValueKind.Array)
            throw new IOException("Official Telegram chat listing failed.");
        var found=new List<LiveGroup>();
        foreach(var id in ids.EnumerateArray())
        {
            if(found.Count>=50)break;
            if(!id.TryGetInt64(out var chatId))continue;
            TdlibEnvelope item;
            try{item=await bridge.SendManualAsync(_liveClient,TdlibManualOperation.GetChat,chatId,null,TimeSpan.FromSeconds(8));}
            catch(TimeoutException){continue;}
            if(item.Type!="chat")continue;
            JsonElement data=item.Body;
            if(!data.TryGetProperty("type",out var typ)||
               !typ.TryGetProperty("@type",out var type))continue;
            string? kind=type.GetString();
            if(kind is not ("chatTypeBasicGroup" or "chatTypeSupergroup"))continue;
            if(kind=="chatTypeSupergroup"&&typ.TryGetProperty("is_channel",out var isChannel)&&isChannel.ValueKind==JsonValueKind.True)
                continue;
            bool allowed=data.TryGetProperty("permissions",out var permissions)&&
                permissions.TryGetProperty("can_send_basic_messages",out var sendFlag)&&sendFlag.ValueKind==JsonValueKind.True;
            var title=data.TryGetProperty("title",out var t)&&t.ValueKind==JsonValueKind.String?t.GetString()??"群组":"群组";
            found.Add(new LiveGroup(chatId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                title.Length>100?title[..100]:title,allowed));
        }
        _liveGroups=found;
        return LiveStatus();
    }

    private async Task<object> LiveSendTextAsync(JsonElement payload)
    {
        if(_liveAuth?.Snapshot.Phase!=AuthPhase.Ready || _liveTransport is null)
            throw new InvalidOperationException("Unauthorized client.");
        if(_acknowledgement is not null)throw new InvalidOperationException("Previous send is unresolved.");
        if(DateTimeOffset.UtcNow<_nextManualSend)throw new InvalidOperationException("Manual rate limit.");
        string chatId=payload.GetProperty("chatId").GetString()!;
        string message=payload.GetProperty("text").GetString()!;
        if(!long.TryParse(chatId,out var id)||id>=0||id==0)
            throw new ArgumentException("Only explicitly selected group chats are supported.");
        if(!_liveGroups.Any(x=>x.ChatId==chatId&&x.CanSend))
            throw new InvalidOperationException("Group is not on the authorized, refreshed list.");
        // Re-fetch server-backed chat permissions immediately before sending.
        var check=await _liveTransport.SendManualAsync(_liveClient,TdlibManualOperation.GetChat,
            id,null,TimeSpan.FromSeconds(10));
        if(check.Type!="chat"||!check.Body.TryGetProperty("permissions",out var permissions)||
           !permissions.TryGetProperty("can_send_basic_messages",out var canSend)||
           canSend.ValueKind!=JsonValueKind.True)
            throw new InvalidOperationException("Group is no longer permitted.");
        _nextManualSend=DateTimeOffset.UtcNow.AddSeconds(30);
        _lastDelivery="请求已提交，等待Telegram确认";
        _acknowledgement=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var result=await _liveTransport.SendManualAsync(_liveClient,TdlibManualOperation.SendOneText,
                id,message,TimeSpan.FromSeconds(20));
            if(result.Type!="message"||!result.Body.TryGetProperty("id",out var tempId)||
               !tempId.TryGetInt64(out var msgId))
            {
                _lastDelivery="提交结果未知，禁止自动重发";
                return new{confirmed=false,review=true,message=_lastDelivery};
            }
            _pendingMessageId=msgId;_pendingChatId=id;
            var confirmation=await _acknowledgement.Task.WaitAsync(TimeSpan.FromSeconds(20));
            return new{confirmed=confirmation,review=false,message=_lastDelivery};
        }
        catch(TimeoutException)
        {
            _lastDelivery="等待超时，发送结果未知，必须人工确认，禁止重试";
            return new{confirmed=false,review=true,message=_lastDelivery};
        }
        finally
        {
            _acknowledgement=null;_pendingMessageId=0;_pendingChatId=0;
        }
    }

    private async Task LiveStopAsync()
    {
        await _liveMutex.WaitAsync();
        try
        {
            _liveClosing=true;_liveGroups.Clear();
            var bridge=_liveTransport;_liveTransport=null;_liveAuth=null;_liveClient=0;
            _acknowledgement?.TrySetCanceled();
            if(bridge is not null)await bridge.DisposeAsync();
            _lastDelivery="连接已关闭";_nativeIssue=null;
        }
        finally{_liveMutex.Release();}
    }
}
