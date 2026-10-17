using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

namespace Mosen.GroupScheduler.Telegram;

public enum TdlibControlCommand { GetVersion, GetAuthorizationState }
public enum TdlibManualOperation { LoadChats, ListChats, GetChat, SendOneText, GetMe }

public sealed record TdlibEnvelope(int ClientId, string Type, JsonElement Body)
{
    public string? Correlation =>
        Body.TryGetProperty("@extra", out JsonElement extra) && extra.ValueKind == JsonValueKind.String
            ? extra.GetString() : null;
}

public sealed class TdlibDispatcher : IAsyncDisposable
{
    private const int MaxClientsForTransportFoundation = 300; // metadata ceiling, not online-capacity claim
    private readonly ITdlibTransport _transport;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<bool>> _closed = new();
    private readonly ConcurrentDictionary<int,byte> _closeRequested = new();
    private readonly ConcurrentDictionary<string, PendingRequest> _pending = new(StringComparer.Ordinal);
    private readonly Channel<TdlibEnvelope> _updates =
        Channel.CreateBounded<TdlibEnvelope>(new BoundedChannelOptions(256)
        {
            SingleWriter = true, SingleReader = true, FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });
    private readonly CancellationTokenSource _stop = new();
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _closeGrace;
    private Task? _pump;
    private readonly object _sendGate = new();
    private long _sequence;
    private int _started, _shutdown, _pumpStarted;

    private sealed record PendingRequest(int ClientId, TaskCompletionSource<TdlibEnvelope> Completion);

    public TdlibDispatcher(ITdlibTransport transport, TimeSpan? pollInterval = null, TimeSpan? closeGrace = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(50);
        _closeGrace = closeGrace ?? TimeSpan.FromSeconds(4);
        if (_pollInterval < TimeSpan.FromMilliseconds(1) || _pollInterval > TimeSpan.FromSeconds(1))
            throw new ArgumentOutOfRangeException(nameof(pollInterval));
        if (_closeGrace < TimeSpan.Zero || _closeGrace > TimeSpan.FromSeconds(30))
            throw new ArgumentOutOfRangeException(nameof(closeGrace));
    }

    public ChannelReader<TdlibEnvelope> Updates => _updates.Reader;
    public int PendingRequestCount => _pending.Count;
    public int RegisteredClientCount => _closed.Count;
    public int ConfirmedClosedCount => _closed.Values.Count(c => c.Task.IsCompletedSuccessfully);

    public int RegisterClient()
    {
        EnsureActive();
        if (_closed.Count >= MaxClientsForTransportFoundation)
            throw new InvalidOperationException("Registered TDLib client identifier limit reached.");
        int id = _transport.CreateClientId();
        if (id <= 0 || !_closed.TryAdd(id,new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)))
            throw new InvalidDataException("TDLib returned an invalid or duplicate client ID.");
        return id;
    }

    public void Start()
    {
        EnsureActive();
        if (Interlocked.CompareExchange(ref _started,1,0) != 0)
            throw new InvalidOperationException("A process may have only one active TDLib receive loop.");
        // TDLib documentation requires a first request before td_receive is called.
        // Launch the single receive loop after the first actual td_send, not here.
    }

    private void StartPumpAfterFirstSend()
    {
        if (Interlocked.CompareExchange(ref _pumpStarted,1,0)==0)
            _pump=Task.Run(ReceiveLoopAsync);
    }

    public async Task<TdlibEnvelope> SendControlAsync(int clientId, TdlibControlCommand command,
        TimeSpan timeout, CancellationToken cancellationToken=default)
    {
        EnsureActive();
        if (Volatile.Read(ref _started) == 0) throw new InvalidOperationException("Start receiver first.");
        if (!_closed.ContainsKey(clientId)) throw new ArgumentException("Unknown client identifier.",nameof(clientId));
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(30))
            throw new ArgumentOutOfRangeException(nameof(timeout));
        cancellationToken.ThrowIfCancellationRequested();

        string id = "ms09-" + Interlocked.Increment(ref _sequence).ToString(System.Globalization.CultureInfo.InvariantCulture);
        string json = command switch
        {
            TdlibControlCommand.GetVersion => JsonSerializer.Serialize(new Dictionary<string,object> {
                ["@type"]="getOption", ["name"]="version", ["@extra"]=id
            }),
            TdlibControlCommand.GetAuthorizationState => JsonSerializer.Serialize(new Dictionary<string,object> {
                ["@type"]="getAuthorizationState", ["@extra"]=id
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
        var completion = new TaskCompletionSource<TdlibEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(id,new PendingRequest(clientId, completion)))
            throw new InvalidOperationException("Duplicate correlation ID.");
        try
        {
            lock(_sendGate)
            {
                EnsureActive();
                _transport.Send(clientId,json);
                StartPumpAfterFirstSend();
            }
            // Timeouts are terminal: no automatic resend or mutation of authenticated state.
            return await completion.Task.WaitAsync(timeout,cancellationToken);
        }
        finally { _pending.TryRemove(id,out _); }
    }

    /// <summary>Auth functions are factory-built and allowlisted; raw JSON is not exposed to the GUI.</summary>
    public Task<TdlibEnvelope> SendAuthorizationAsync(int clientId, TdlibAuthRequest request,
        TimeSpan timeout, CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendCorrelatedAsync(clientId, request.Build, timeout, cancellationToken);
    }

    private async Task<TdlibEnvelope> SendCorrelatedAsync(int clientId,Func<string,string> encode,
        TimeSpan timeout,CancellationToken cancellationToken)
    {
        EnsureActive();
        if(Volatile.Read(ref _started)==0)throw new InvalidOperationException("Start receiver first.");
        if(!_closed.ContainsKey(clientId))throw new ArgumentException("Unknown client identifier.",nameof(clientId));
        if(timeout<=TimeSpan.Zero||timeout>TimeSpan.FromSeconds(30))throw new ArgumentOutOfRangeException(nameof(timeout));
        cancellationToken.ThrowIfCancellationRequested();
        string id="ms10-"+Interlocked.Increment(ref _sequence).ToString(System.Globalization.CultureInfo.InvariantCulture);
        string json=encode(id); // build before placing in dictionary; no auth value ever logged
        var completion=new TaskCompletionSource<TdlibEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        if(!_pending.TryAdd(id,new PendingRequest(clientId,completion)))throw new InvalidOperationException("Duplicate correlation ID");
        try
        {
            lock(_sendGate){EnsureActive();_transport.Send(clientId,json);StartPumpAfterFirstSend();}
            return await completion.Task.WaitAsync(timeout,cancellationToken);
        }
        finally{_pending.TryRemove(id,out _);}
    }

    /// <summary>One manual operation, correlated to a single TDLib client; no automatic retry.</summary>
    public Task<TdlibEnvelope> SendManualAsync(int clientId,TdlibManualOperation command,long chatId,
        string? text,TimeSpan timeout,CancellationToken cancellationToken=default)
    {
        return SendCorrelatedAsync(clientId,id=>command switch
        {
            TdlibManualOperation.LoadChats => TdlibManualOperations.LoadChats(id),
            TdlibManualOperation.ListChats => TdlibManualOperations.Chats(id),
            TdlibManualOperation.GetChat => TdlibManualOperations.Chat(id,chatId),
            TdlibManualOperation.GetMe => TdlibManualOperations.Me(id),
            TdlibManualOperation.SendOneText => TdlibManualOperations.SendOneText(id,chatId,text!),
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        },timeout,cancellationToken);
    }

    /// <summary>Explicit one-client close used to cancel authorization, not logOut.</summary>
    public Task CloseAuthClientAsync(int clientId,CancellationToken cancellationToken=default)
    {
        EnsureActive();
        cancellationToken.ThrowIfCancellationRequested();
        if(!_closed.ContainsKey(clientId))throw new ArgumentException("Unknown client identifier.",nameof(clientId));
        lock(_sendGate)
        {
            EnsureActive();
            if (!_closeRequested.TryAdd(clientId,0)) return Task.CompletedTask;
            try {_transport.Send(clientId,"""{"@type":"close"}""");StartPumpAfterFirstSend();}
            catch {_closeRequested.TryRemove(clientId,out _);throw;}
        }
        return Task.CompletedTask;
    }

    private async Task ReceiveLoopAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                string? raw = _transport.Receive(_pollInterval);
                if (raw is null) { await Task.Yield(); continue; }
                if (raw.Length > 1024 * 1024) continue;
                TdlibEnvelope? envelope = TryReadEnvelope(raw);
                if (envelope is null || !_closed.ContainsKey(envelope.ClientId)) continue;
                if (envelope.Type == "updateAuthorizationState" &&
                    envelope.Body.TryGetProperty("authorization_state",out JsonElement state) &&
                    state.ValueKind == JsonValueKind.Object &&
                    state.TryGetProperty("@type",out JsonElement kind) &&
                    kind.ValueKind == JsonValueKind.String &&
                    kind.GetString() == "authorizationStateClosed" &&
                    _closed.TryGetValue(envelope.ClientId,out var closed))
                    closed.TrySetResult(true);
                // @extra must match BOTH request id and TDLib client id.
                if (envelope.Correlation is string correlation)
                {
                    if (_pending.TryGetValue(correlation,out var candidate)
                        && candidate.ClientId == envelope.ClientId
                        && _pending.TryRemove(correlation,out var active))
                        active.Completion.TrySetResult(envelope);
                    // Late or unsolicited responses are never promoted to updates.
                    continue;
                }
                // Backpressure preserves received order; never launch per-update tasks.
                await _updates.Writer.WriteAsync(envelope,_stop.Token);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception)
        {
            // Do not log raw JSON, account names, exception messages or credentials.
            foreach (var p in _pending.Values)
                p.Completion.TrySetException(new IOException("TDLib receive transport stopped unexpectedly."));
            _updates.Writer.TryComplete(new IOException("TDLib receive transport stopped unexpectedly."));
            return;
        }
        finally { _updates.Writer.TryComplete(); }
    }

    private static TdlibEnvelope? TryReadEnvelope(string raw)
    {
        try
        {
            using JsonDocument parsed = JsonDocument.Parse(raw,new JsonDocumentOptions {MaxDepth=64});
            JsonElement doc=parsed.RootElement;
            if (doc.ValueKind != JsonValueKind.Object ||
                !doc.TryGetProperty("@type",out JsonElement type) ||
                type.ValueKind != JsonValueKind.String ||
                !doc.TryGetProperty("@client_id",out JsonElement client) ||
                client.ValueKind != JsonValueKind.Number ||
                !client.TryGetInt32(out int clientId) || clientId <= 0)
                return null;
            return new TdlibEnvelope(clientId,type.GetString()!,doc.Clone());
        }
        catch (JsonException) { return null; }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _shutdown,1) != 0) return;
        // Modern TDLib requires a "close" for *each* client. Actual native cleanup
        // is only confirmed after authorizationStateClosed updates.
        foreach (int client in _closed.Keys.Order())
        {
            try
            {
                lock(_sendGate)
                {
                    if (_closeRequested.TryAdd(client,0))
                    {
                        try {_transport.Send(client, """{"@type":"close"}""");StartPumpAfterFirstSend();}
                        catch {_closeRequested.TryRemove(client,out _);throw;}
                    }
                }
            }
            catch (Exception) { /* best effort; report incomplete close */ }
        }
        if (_pump is not null && _closed.Count>0 && _closeGrace>TimeSpan.Zero)
        {
            try { await Task.WhenAll(_closed.Values.Select(v=>v.Task)).WaitAsync(_closeGrace); }
            catch (TimeoutException) { /* don't hang the desktop on a stalled native client */ }
        }
        _stop.Cancel();
        if (_pump is not null) await _pump;
        foreach (var pending in _pending.Values)
            pending.Completion.TrySetException(new ObjectDisposedException(nameof(TdlibDispatcher)));
        _pending.Clear();
        _updates.Writer.TryComplete();
        _transport.Dispose();
        _stop.Dispose();
    }

    private void EnsureActive()
    {
        if (Volatile.Read(ref _shutdown)!=0)throw new ObjectDisposedException(nameof(TdlibDispatcher));
    }
}
