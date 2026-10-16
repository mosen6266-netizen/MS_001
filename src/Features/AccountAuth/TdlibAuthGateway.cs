using Mosen.GroupScheduler.Telegram;

namespace Mosen.GroupScheduler.AccountAuth;

/// <summary>
/// Real TDLib routing is deliberately not connected to WPF until native binary and
/// credential vault pass launch gates. No secret input is put in diagnostic events.
/// </summary>
public sealed class TdlibAuthGateway : IAuthGateway
{
    private readonly TdlibDispatcher _dispatcher;
    private readonly TdlibAuthConfiguration _configuration;
    public TdlibAuthGateway(TdlibDispatcher dispatcher,TdlibAuthConfiguration configuration)
    {
        _dispatcher=dispatcher??throw new ArgumentNullException(nameof(dispatcher));
        _configuration=configuration??throw new ArgumentNullException(nameof(configuration));
    }
    public Task<TdlibEnvelope> ExecuteAsync(int clientId,AuthCommand command,TimeSpan timeout,CancellationToken ct)
    {
        TdlibAuthRequest request=command.Kind switch
        {
            AuthCommandKind.Configure=>TdlibAuthRequest.Configure(_configuration),
            AuthCommandKind.StartQr=>TdlibAuthRequest.RequestQr(),
            AuthCommandKind.RefreshQr=>TdlibAuthRequest.RefreshQr(),
            AuthCommandKind.EnterPhone=>TdlibAuthRequest.Phone(command.Input!),
            AuthCommandKind.EnterCode=>TdlibAuthRequest.Code(command.Input!),
            AuthCommandKind.EnterEmailAddress=>TdlibAuthRequest.EmailAddress(command.Input!),
            AuthCommandKind.EnterEmailCode=>TdlibAuthRequest.EmailCode(command.Input!),
            AuthCommandKind.EnterPassword=>TdlibAuthRequest.Password(command.Input!),
            AuthCommandKind.Register=>TdlibAuthRequest.Registration(command.Input!,command.LastName!),
            _=>throw new InvalidOperationException("Cancel must close the TDLib client, not send credentials.")
        };
        return _dispatcher.SendAuthorizationAsync(clientId,request,timeout,ct);
    }
    public Task CloseAsync(int clientId,CancellationToken ct)=>
        _dispatcher.CloseAuthClientAsync(clientId,ct);
}
