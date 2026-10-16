using Mosen.GroupScheduler.Telegram;

namespace Mosen.GroupScheduler.AccountAuth;

/// <summary>
/// One authorized user's authentication attempt per TDLib client ID.
/// Driven by official updateAuthorizationState events; no optimistic Ready.
/// No auto retries, no bypass of FloodWait/Premium and no implicit consent.
/// </summary>
public interface IAuthGateway
{
    Task<TdlibEnvelope> ExecuteAsync(int clientId,AuthCommand command,TimeSpan timeout,CancellationToken cancellationToken);
    Task CloseAsync(int clientId,CancellationToken cancellationToken);
}
public sealed class AuthSession
{
    private readonly IAuthGateway _gateway;
    private readonly object _gate=new();
    private readonly int _clientId;
    private AuthView _view=AuthStateReader.Initial;
    private int _busy;
    private bool _cancelled;
    private bool _qrPending;
    private const int MaxInputLength=512;

    public AuthSession(int clientId,IAuthGateway gateway)
    {
        if(clientId<=0)throw new ArgumentOutOfRangeException(nameof(clientId));
        _clientId=clientId;_gateway=gateway??throw new ArgumentNullException(nameof(gateway));
    }
    public int ClientId=>_clientId;
    public AuthView Snapshot { get {lock(_gate)return _view;} }

    public bool Accept(TdlibEnvelope envelope)
    {
        if(envelope.ClientId!=_clientId || envelope.Type!="updateAuthorizationState")return false;
        AuthView next;
        try {next=AuthStateReader.FromUpdate(envelope,0);}
        catch(InvalidDataException){return false;}
        lock(_gate)
        {
            if(_cancelled && next.Phase!=AuthPhase.Closed)return false;
            if(_view.Phase==AuthPhase.Closed)return false; // closed clients never resurrect
            if(_view.Phase==next.Phase && _view.QrLink==next.QrLink)return false; // duplicate QR/update doesn't spawn another client
            if(next.Phase!=AuthPhase.WaitPhoneOrQr) _qrPending=next.Phase==AuthPhase.WaitQrConfirmation;
            else _qrPending=false;
            _view=next with {Revision=_view.Revision+1};
            return true;
        }
    }

    public async Task<AuthResult> SubmitAsync(AuthCommand command,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Cancellation is an emergency action: must work even if a previous auth query is pending.
        if(command.Kind==AuthCommandKind.Cancel)
        {
            lock(_gate)
            {
                if(_cancelled || !Allowed(_view.Phase,AuthCommandKind.Cancel))
                    return new(false,"CLOSED","此登录流程已取消或正在关闭。");
                _cancelled=true;
                _view=new(AuthPhase.Closing,"已取消登录","正在关闭当前客户端。",false,false,false,false,null,_view.Revision+1);
            }
            try {await _gateway.CloseAsync(_clientId,cancellationToken);}
            catch(Exception){return new(false,"CLOSE_UNCERTAIN","关闭请求结果不确定，不得自动创建另一个会话。");}
            return new(true,"CANCELLED","当前登录尝试已取消，不会自动再次扫码。");
        }
        if(Interlocked.CompareExchange(ref _busy,1,0)!=0)
            return new(false,"BUSY","当前验证请求尚未完成，请勿重复提交。");
        try
        {
            AuthView before;
            lock(_gate)
            {
                if(_cancelled)return new(false,"CLOSED","此登录流程已取消，不能重复使用。");
                before=_view;
                if(command.Kind==AuthCommandKind.StartQr && _qrPending)
                    return new(false,"QR_PENDING","二维码请求已发送，请等待 Telegram 更新链接。");
            }
            if(!Allowed(before.Phase,command.Kind))
                return new(false,"WRONG_STATE","当前 Telegram 验证状态不允许此操作。");
            if(!ValidInput(command))
                return new(false,"INVALID","请输入有效内容，并确保使用自己的账号。");
            TdlibEnvelope answer;
            try
            {
                answer=await _gateway.ExecuteAsync(_clientId,command,TimeSpan.FromSeconds(15),cancellationToken);
            }
            catch(OperationCanceledException){return new(false,"INTERRUPTED","操作已中止，请先查看当前授权状态，避免盲目重试。");}
            catch(TimeoutException){return new(false,"UNKNOWN_OUTCOME","请求结果不确定，请先刷新授权状态，勿重复提交。");}
            catch(Exception){return new(false,"UNAVAILABLE","无法连接 Telegram 授权服务，请检查本地连接。");}
            lock(_gate)
            {
                if(_cancelled)return new(false,"CLOSED","此流程已关闭。");
            }
            if(answer.Type=="error")
            {
                int code=answer.Body.TryGetProperty("code",out var value)&&value.ValueKind==System.Text.Json.JsonValueKind.Number&&value.TryGetInt32(out var n)?n:0;
                return code==429?new(false,"RATE_LIMIT","Telegram 限制了请求频率，请遵守官方等待提示。"):
                    new(false,"REJECTED","Telegram 未接受本次验证，请检查输入或官方提示。");
            }
            if(answer.Type!="ok" && command.Kind!=AuthCommandKind.RefreshQr &&
                answer.Type!="authorizationStateWaitOtherDeviceConfirmation")
                return new(false,"UNEXPECTED","收到意外的授权响应，请先核查状态。");
            if(command.Kind==AuthCommandKind.StartQr)
                lock(_gate){_qrPending=true;}
            return new(true,"SUBMITTED","请求已提交，请等待 Telegram 官方授权状态更新。");
        }
        finally {Volatile.Write(ref _busy,0);}
    }
    public static bool Allowed(AuthPhase phase,AuthCommandKind action) =>
        action switch
        {
            AuthCommandKind.Cancel=>phase is not (AuthPhase.Closing or AuthPhase.Closed or AuthPhase.LoggingOut),
            AuthCommandKind.Configure=>phase==AuthPhase.WaitParameters,
            AuthCommandKind.StartQr=>phase==AuthPhase.WaitPhoneOrQr,
            AuthCommandKind.RefreshQr=>phase==AuthPhase.WaitQrConfirmation,
            AuthCommandKind.EnterPhone=>phase==AuthPhase.WaitPhoneOrQr,
            AuthCommandKind.EnterCode=>phase==AuthPhase.WaitCode,
            AuthCommandKind.EnterEmailAddress=>phase==AuthPhase.WaitEmailAddress,
            AuthCommandKind.EnterEmailCode=>phase==AuthPhase.WaitEmailCode,
            AuthCommandKind.EnterPassword=>phase==AuthPhase.WaitPassword,
            AuthCommandKind.Register=>phase==AuthPhase.WaitRegistration,
            _=>false
        };

    private static bool ValidInput(AuthCommand c) => c.Kind switch
    {
        AuthCommandKind.Cancel or AuthCommandKind.StartQr or AuthCommandKind.RefreshQr or AuthCommandKind.Configure=>c.Input is null,
        AuthCommandKind.EnterPhone=>c.Input is {Length:>=7 and <=20} number
            && number[0]=='+' && number.Skip(1).All(char.IsAsciiDigit),
        AuthCommandKind.EnterCode or AuthCommandKind.EnterEmailCode=>c.Input is {Length:>=2 and <=32} code && code.All(char.IsLetterOrDigit),
        AuthCommandKind.EnterEmailAddress=>c.Input is {Length:>=3 and <=254} email &&
            email.IndexOf('@')>0 && email.LastIndexOf('.')>email.IndexOf('@')+1 && !email.Any(char.IsWhiteSpace),
        AuthCommandKind.EnterPassword=>c.Input is {Length:>=1 and <=MaxInputLength},
        AuthCommandKind.Register=>c.AcceptedTerms && c.Input is {Length:>=1 and <=64} first &&
            !string.IsNullOrWhiteSpace(first) && c.LastName is {Length:<=64},
        _=>false
    };
}
