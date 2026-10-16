using Mosen.GroupScheduler.Groups;
using Mosen.GroupScheduler.StateMachine;

namespace Mosen.GroupScheduler.OutboundPolicy;

public sealed record SendAuthority(Guid AccountId,bool LiveAuthorized,DateTimeOffset AuthorizedUtc,
    GroupInfo? Group,TopicInfo? Topic,MembershipInfo? Membership,
    bool RefreshedForThisSend,bool NeedsText,bool NeedsPhoto);
public enum PlatformLimitScope { Account, Chat }
public sealed record OutboundPermit(Guid Token,string RunId,string StepKey,Guid AccountId,string ChatId,
    string? TopicId,DateTimeOffset ReservedUtc);
public sealed record OutboundDecision(bool Allowed,string Code,string Message,
    DateTimeOffset? RetryAtUtc=null,OutboundPermit? Permit=null)
{
    public static OutboundDecision Block(string code,string message,DateTimeOffset? retry=null)=>
        new(false,code,message,retry);
}

/// <summary>
/// Offline policy gate only: no Telegram client reference, sender callback, background scheduler,
/// alternate-account selection or retry. Callers must supply fresh trusted platform facts.
/// </summary>
public sealed class OutboundCoordinator
{
    private readonly object _gate=new();
    private readonly Dictionary<Guid,OutboundPermit> _inFlight=new();
    private readonly HashSet<Guid> _unknown=new();
    private readonly Dictionary<Guid,DateTimeOffset> _accountUntil=new();
    private readonly Dictionary<string,DateTimeOffset> _chatUntil=new(StringComparer.Ordinal);
    private readonly Dictionary<string,DateTimeOffset> _accountChatUntil=new(StringComparer.Ordinal);
    private readonly Dictionary<Guid,Queue<DateTimeOffset>> _budgets=new();
    private readonly TimeSpan _accountSpacing;
    private readonly TimeSpan _chatSpacing;
    private readonly int _maxAccountMinute;
    private bool _closed;

    public OutboundCoordinator(TimeSpan? accountSpacing=null,TimeSpan? chatSpacing=null,int maxAccountMinute=6)
    {
        _accountSpacing=accountSpacing??TimeSpan.FromSeconds(3);
        _chatSpacing=chatSpacing??TimeSpan.FromSeconds(3);
        if(_accountSpacing<TimeSpan.FromSeconds(1)||_chatSpacing<TimeSpan.FromSeconds(1)||
            maxAccountMinute is <1 or >6)
            throw new ArgumentOutOfRangeException(nameof(maxAccountMinute),"Safety floors cannot be reduced.");
        _maxAccountMinute=maxAccountMinute;
    }
    private static string Pair(Guid id,string chatId)=>id.ToString("D")+"|"+chatId;
    private static DateTimeOffset Max(DateTimeOffset a,DateTimeOffset b)=>a>b?a:b;
    private static void Extend<TKey>(Dictionary<TKey,DateTimeOffset> map,TKey key,DateTimeOffset until)
        where TKey:notnull
    {
        if(!map.TryGetValue(key,out var prior)||until>prior)map[key]=until;
    }

    /// <summary>Reservation is a single-use *simulation permission*, never a message transmit action.</summary>
    public OutboundDecision TryReserve(SchedulerJob job,SendAuthority proof,DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(proof);
        lock(_gate)
        {
            if(_closed)return OutboundDecision.Block("SHUTDOWN","程序已停止，不得继续出站。");
            if(job.Status!=JobStatus.Running||job.Phase!=JobPhase.AwaitingSend ||
               job.NextStepIndex<0||job.NextStepIndex>=job.Steps.Count)
                return OutboundDecision.Block("NOT_READY","消息未达到发送阶段，不能提前占用发送许可。");
            var step=job.Steps[job.NextStepIndex];
            if(proof.AccountId==Guid.Empty||proof.AccountId!=step.AccountId||
               proof.Group?.ChatId!=job.ChatId||proof.Membership?.AccountId!=step.AccountId ||
               proof.Membership?.ChatId!=job.ChatId ||
               (job.TopicId is null?proof.Topic is not null:
                    proof.Topic?.TopicId!=job.TopicId||proof.Topic.ChatId!=job.ChatId))
                return OutboundDecision.Block("BINDING_MISMATCH","账号、群组或话题与启动时冻结的绑定不符。");
            if(!proof.LiveAuthorized||!proof.RefreshedForThisSend||
               now<proof.AuthorizedUtc||now-proof.AuthorizedUtc>TimeSpan.FromSeconds(30))
                return OutboundDecision.Block("AUTH_REFRESH_REQUIRED","发言前必须重新确认此账号的实时授权。");
            var permissions=GroupPermissionPolicy.Preflight(proof.Group,proof.Topic,proof.Membership,
                proof.NeedsPhoto,now,proof.RefreshedForThisSend);
            if(!permissions.Allowed || proof.NeedsText && proof.Membership?.CanSendText!=true)
                return OutboundDecision.Block(permissions.Allowed?"NO_TEXT_PERMISSION":permissions.Code,
                    permissions.Allowed?"当前账号没有发送文字权限。":permissions.Message);
            if(!proof.NeedsText&&!proof.NeedsPhoto)
                return OutboundDecision.Block("EMPTY_MESSAGE","空消息不能提交。");
            if(_inFlight.TryGetValue(proof.AccountId,out _))
                return OutboundDecision.Block(_unknown.Contains(proof.AccountId)?"OUTCOME_UNKNOWN":"ACCOUNT_BUSY",
                    "此账号有未确认的消息，不得使用其他群或任务并发发送。");
            var earliest=now;
            if(_accountUntil.TryGetValue(proof.AccountId,out var accountUntil))earliest=Max(earliest,accountUntil);
            if(_chatUntil.TryGetValue(job.ChatId,out var chatUntil))earliest=Max(earliest,chatUntil);
            var pair=Pair(proof.AccountId,job.ChatId);
            if(_accountChatUntil.TryGetValue(pair,out var groupUntil))earliest=Max(earliest,groupUntil);
            if(_budgets.TryGetValue(proof.AccountId,out var history))
            {
                while(history.Count>0&&now-history.Peek()>=TimeSpan.FromMinutes(1))history.Dequeue();
                if(history.Count>=_maxAccountMinute)
                    earliest=Max(earliest,history.Peek().AddMinutes(1));
            }
            if(earliest>now)
                return OutboundDecision.Block("WAIT_PLATFORM_LIMIT","等待平台限制或安全频率预算到期。",earliest);
            var permit=new OutboundPermit(Guid.NewGuid(),job.RunId,step.StepKey,step.AccountId,
                job.ChatId,job.TopicId,now);
            _inFlight.Add(proof.AccountId,permit);
            if(history is null){history=new Queue<DateTimeOffset>();_budgets[proof.AccountId]=history;}
            history.Enqueue(now); // a failed/uncertain request still consumes a conservative budget slot
            Extend(_accountUntil,proof.AccountId,now+_accountSpacing);
            Extend(_chatUntil,job.ChatId,now+_chatSpacing);
            if(permissions.SlowModeSeconds>0)
                Extend(_accountChatUntil,pair,now+TimeSpan.FromSeconds(permissions.SlowModeSeconds));
            return new(true,"RESERVED","仅授予一次模拟发送许可；未调用 Telegram。",Permit:permit);
        }
    }
    public void ApplyPlatformLimit(Guid accountId,string chatId,PlatformLimitScope scope,
        TimeSpan retryAfter,DateTimeOffset now)
    {
        if(accountId==Guid.Empty||string.IsNullOrWhiteSpace(chatId)||
            retryAfter<=TimeSpan.Zero||retryAfter>TimeSpan.FromDays(7))
            throw new ArgumentException("Invalid official rate-limit notice.");
        lock(_gate)
        {
            if(_closed)return;
            var until=now+retryAfter;
            // Slow mode affects the whole chat, preventing alternate-account bypass.
            // Flood wait affects this account across every group and its affected chat.
            if(scope==PlatformLimitScope.Account)
            {
                Extend(_accountUntil,accountId,until);
                Extend(_chatUntil,chatId,until);
            }
            else if(scope==PlatformLimitScope.Chat)
                Extend(_chatUntil,chatId,until);
            else throw new ArgumentException("Unrecognized limit scope.");
        }
    }
    /// <summary>Acknowledged success or *definitive* non-delivery releases only its exact ticket.</summary>
    public bool Settle(OutboundPermit ticket,bool definitelyResolved)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        lock(_gate)
        {
            if(!_inFlight.TryGetValue(ticket.AccountId,out var reserved)||reserved.Token!=ticket.Token)
                return false;
            if(!definitelyResolved)
            {
                _unknown.Add(ticket.AccountId);
                return true;
            }
            // Unknown delivery cannot be cleared by treating it as failed.
            if(_unknown.Contains(ticket.AccountId))return false;
            _inFlight.Remove(ticket.AccountId);
            return true;
        }
    }
    public void Shutdown()
    {
        lock(_gate){_closed=true;_inFlight.Clear();_unknown.Clear();}
    }
}
