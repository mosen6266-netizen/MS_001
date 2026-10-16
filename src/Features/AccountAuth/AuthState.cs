using System.Text.Json;
using Mosen.GroupScheduler.Telegram;

namespace Mosen.GroupScheduler.AccountAuth;

public enum AuthPhase
{
    Initializing, WaitParameters, WaitPhoneOrQr, WaitQrConfirmation,
    WaitCode, WaitEmailAddress, WaitEmailCode, WaitPassword,
    WaitRegistration, WaitPremiumPurchase, Ready, LoggingOut, Closing, Closed, Unsupported
}
public enum AuthCommandKind
{
    Configure, StartQr, RefreshQr, EnterPhone, EnterCode, EnterEmailAddress,
    EnterEmailCode, EnterPassword, Register, Cancel
}
public sealed class AuthCommand
{
    // Input is transient. Never print its value, serialize this object into diagnostics
    // or persist it to SQLite, error messages or script exports.
    public AuthCommandKind Kind { get; }
    public string? Input { get; }
    public string? LastName { get; }
    public bool AcceptedTerms { get; }
    public AuthCommand(AuthCommandKind kind, string? input=null, string? lastName=null, bool acceptedTerms=false)
    { Kind=kind; Input=input; LastName=lastName; AcceptedTerms=acceptedTerms; }
    public override string ToString() => "AuthCommand(" + Kind + "; value=REDACTED)";
}
public sealed record AuthView(
    AuthPhase Phase, string Title, string Instruction, bool CanSubmit,
    bool CanStartQr, bool CanRefreshQr, bool CanCancel, string? QrLink, long Revision)
{
    // Never use generated record ToString here: QR deep-link is a bearer-like token.
    public override string ToString() => "AuthView(" + Phase + "; private=REDACTED)";
}
public sealed record AuthResult(bool Accepted, string Code, string Message);

/// <summary>Provides only non-sensitive Chinese guidance, covering the official TDLib authorization states.</summary>
public static class AuthStateReader
{
    public static AuthView FromUpdate(TdlibEnvelope envelope,long revision)
    {
        if (envelope.Type!="updateAuthorizationState" ||
            !envelope.Body.TryGetProperty("authorization_state",out JsonElement state) ||
            state.ValueKind!=JsonValueKind.Object ||
            !state.TryGetProperty("@type",out JsonElement type) || type.ValueKind!=JsonValueKind.String)
            throw new InvalidDataException("Expected TDLib authorization update.");
        string kind=type.GetString()??"";
        string? link=null;
        if(kind=="authorizationStateWaitOtherDeviceConfirmation" &&
            state.TryGetProperty("link",out JsonElement token) && token.ValueKind==JsonValueKind.String)
        {
            string? value=token.GetString();
            if(value is not null && value.StartsWith("tg://",StringComparison.Ordinal) && value.Length<=4096)
                link=value; // volatile UI memory only, never persisted or logged.
        }
        return kind switch
        {
            "authorizationStateWaitTdlibParameters" => View(AuthPhase.WaitParameters,"配置连接",
                "需要在本机完成合法的 Telegram API 参数和加密会话目录配置。",true,false,false,true),
            "authorizationStateWaitPhoneNumber" => View(AuthPhase.WaitPhoneOrQr,"选择登录方式",
                "可使用本人手机号接收验证码，或由已登录的 Telegram 手机客户端扫描二维码。",true,true,false,true),
            "authorizationStateWaitOtherDeviceConfirmation" => View(AuthPhase.WaitQrConfirmation,"等待手机确认",
                link is null ? "尚未收到可用的二维码链接，请等待更新。" :
                "请使用已登录的 Telegram 手机客户端扫描二维码并确认。二维码会由 Telegram 更新。",false,false,true,true,link),
            "authorizationStateWaitCode" => View(AuthPhase.WaitCode,"输入登录验证码",
                "请输入 Telegram 官方渠道送达的验证码。请勿把验证码分享给任何人。",true,false,false,true),
            "authorizationStateWaitEmailAddress" => View(AuthPhase.WaitEmailAddress,"验证电子邮箱",
                "按 Telegram 当前要求输入本人可访问的邮箱地址。",true,false,false,true),
            "authorizationStateWaitEmailCode" => View(AuthPhase.WaitEmailCode,"输入邮箱验证码",
                "请输入 Telegram 发送到邮箱的验证码。不要在日志、导出或聊天中保存验证码。",true,false,false,true),
            "authorizationStateWaitPassword" => View(AuthPhase.WaitPassword,"两步验证密码",
                "请输入本人设置的 Telegram 两步验证密码。密码仅用于当前验证请求。",true,false,false,true),
            "authorizationStateWaitRegistration" => View(AuthPhase.WaitRegistration,"注册新账号",
                "Telegram 要求填写姓名并明确接受官方服务条款；不会自动同意。",true,false,false,true),
            "authorizationStateWaitPremiumPurchase" => View(AuthPhase.WaitPremiumPurchase,"需要官方处理",
                "Telegram 要求在官方客户端完成 Premium 购买或联系官方支持；本程序不会绕过要求。",false,false,false,true),
            "authorizationStateReady" => View(AuthPhase.Ready,"授权成功",
                "Telegram 已确认当前客户端授权，后续仍须检查群权限。",false,false,false,true),
            "authorizationStateLoggingOut" => View(AuthPhase.LoggingOut,"正在退出账号",
                "Telegram 正在处理账号退出，请等待最终状态。",false,false,false,false),
            "authorizationStateClosing" => View(AuthPhase.Closing,"正在关闭连接",
                "TDLib 正在关闭本地客户端。",false,false,false,false),
            "authorizationStateClosed" => View(AuthPhase.Closed,"已关闭连接",
                "当前客户端已关闭，需要重新建立受控会话后才可继续。",false,false,false,false),
            _ => View(AuthPhase.Unsupported,"无法继续验证",
                "收到当前软件暂不支持的 Telegram 授权状态，请更新客户端或联系管理员。",false,false,false,true)
        };
        AuthView View(AuthPhase phase,string title,string instruction,bool submit,bool qr,bool refresh,bool cancel,string? qrLink=null)=>
            new(phase,title,instruction,submit,qr,refresh,cancel,qrLink,revision);
    }

    public static AuthView Initial => new(AuthPhase.Initializing,"等待 TDLib","正在等待 Telegram 官方授权状态。",false,false,false,true,null,0);
}
