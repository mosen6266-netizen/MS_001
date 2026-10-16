using Mosen.GroupScheduler.AccountAuth;

namespace Mosen.GroupScheduler.Core.Sessions;
public enum SessionPhase { Dormant, Opening, AwaitingAuthorization, Ready, Closing, NeedsReview, Archived }
public sealed record SessionSnapshot(Guid AccountId,SessionPhase Phase,bool OccupiesConnectionSlot);
public interface IAccountRuntime
{
    /// <summary>Must open a local transport only. Cannot claim authorized until TDLib official Ready.</summary>
    Task OpenAsync(Guid accountId,SessionPaths directories,SessionKeyLease key,CancellationToken ct);
    /// <summary>Must complete only when native resources are confirmed closed, or throw.</summary>
    Task CloseAsync(Guid accountId,CancellationToken ct);
}
/// <summary>Registered metadata != open native clients != officially authenticated sessions.</summary>
public sealed class SessionSupervisor : IAsyncDisposable
{
    private const int MaximumRegistered=300;
    private readonly object _gate=new();
    private readonly SemaphoreSlim _connections;
    private readonly Dictionary<Guid,SessionPhase> _states=new();
    private readonly IAccountRuntime _runtime;
    private readonly SessionVault _vault;
    private bool _disposed;
    public SessionSupervisor(SessionVault vault,IAccountRuntime runtime,int maxActiveConnections=16)
    {
        if(maxActiveConnections is < 1 or > 32)throw new ArgumentOutOfRangeException(nameof(maxActiveConnections));
        _connections=new SemaphoreSlim(maxActiveConnections,maxActiveConnections);
        _vault=vault??throw new ArgumentNullException(nameof(vault));
        _runtime=runtime??throw new ArgumentNullException(nameof(runtime));
        ActiveLimit=maxActiveConnections;
    }
    public int ActiveLimit{get;}
    public int RegisteredCount{get{lock(_gate)return _states.Count;}}
    public int OccupiedSlots{get{lock(_gate)return _states.Values.Count(HasSlot);}}
    public int ConfirmedReady{get{lock(_gate)return _states.Values.Count(s=>s==SessionPhase.Ready);}}
    private static bool HasSlot(SessionPhase s)=>s is SessionPhase.Opening or SessionPhase.AwaitingAuthorization
        or SessionPhase.Ready or SessionPhase.Closing or SessionPhase.NeedsReview;
    public SessionSnapshot Get(Guid accountId)
    {
        lock(_gate)
        {
            if(!_states.TryGetValue(accountId,out var state))throw new KeyNotFoundException("Unknown account slot.");
            return new(accountId,state,HasSlot(state));
        }
    }
    public void Register(Guid id,bool archived=false)
    {
        if(id==Guid.Empty)throw new ArgumentException("Invalid slot ID.");
        lock(_gate)
        {
            ThrowIfDisposed();
            if(_states.Count>=MaximumRegistered)throw new InvalidOperationException("300 registered account slots reached.");
            if(!_states.TryAdd(id,archived?SessionPhase.Archived:SessionPhase.Dormant))
                throw new InvalidOperationException("Duplicate account slot ID.");
        }
    }
    public async Task OpenAsync(Guid id,CancellationToken ct=default)
    {
        lock(_gate)
        {
            ThrowIfDisposed();
            if(!_states.TryGetValue(id,out var state)||state!=SessionPhase.Dormant)
                throw new InvalidOperationException("Account slot is not available for a new connection.");
            _states[id]=SessionPhase.Opening;
        }
        bool acquired=false;
        try
        {
            await _connections.WaitAsync(ct);
            acquired=true;
            lock(_gate){ThrowIfDisposed();}
            _vault.InitializeNew(id);
            _vault.EnsureDirectories(id);
            using var lease=_vault.OpenKey(id);
            await _runtime.OpenAsync(id,_vault.Paths(id),lease,ct);
            lock(_gate){_states[id]=SessionPhase.AwaitingAuthorization;}
        }
        catch
        {
            lock(_gate){_states[id]=acquired?SessionPhase.NeedsReview:SessionPhase.Dormant;}
            if(!acquired)throw;
            // If native opening failed halfway, its close status is unknown. Keep a reserved
            // slot until a confirmed cleanup, preventing double-open and unsafe resource reuse.
            throw;
        }
    }
    public bool ApplyOfficialAuth(Guid id,AuthView official)
    {
        lock(_gate)
        {
            if(!_states.TryGetValue(id,out var state))return false;
            if(state is not (SessionPhase.AwaitingAuthorization or SessionPhase.Ready))return false;
            if(official.Phase==AuthPhase.Ready){_states[id]=SessionPhase.Ready;return true;}
            if(official.Phase is AuthPhase.Closed or AuthPhase.Closing)
            { _states[id]=SessionPhase.NeedsReview;return true; }
            if(state==SessionPhase.Ready){_states[id]=SessionPhase.AwaitingAuthorization;return true;}
            return true;
        }
    }
    public async Task CloseAsync(Guid id,CancellationToken ct=default)
    {
        bool releases;
        lock(_gate)
        {
            ThrowIfDisposed();
            if(!_states.TryGetValue(id,out var state))throw new KeyNotFoundException();
            if(state==SessionPhase.Archived)return;
            if(state==SessionPhase.Dormant)return;
            if(state is SessionPhase.Opening or SessionPhase.Closing)
                throw new InvalidOperationException("Connection is changing state; wait before closing.");
            releases=HasSlot(state);
            _states[id]=SessionPhase.Closing;
        }
        try
        {
            await _runtime.CloseAsync(id,ct);
            lock(_gate){_states[id]=SessionPhase.Dormant;}
            if(releases)_connections.Release();
        }
        catch
        {
            lock(_gate){_states[id]=SessionPhase.NeedsReview;}
            throw;
        }
    }
    public void Archive(Guid id)
    {
        lock(_gate)
        {
            ThrowIfDisposed();
            if(!_states.TryGetValue(id,out var state)||state!=SessionPhase.Dormant)
                throw new InvalidOperationException("Close and confirm all native handles before account archive.");
            _states[id]=SessionPhase.Archived;
        }
    }
    public void Restore(Guid id)
    {
        lock(_gate)
        {
            ThrowIfDisposed();
            if(!_states.TryGetValue(id,out var state)||state!=SessionPhase.Archived)
                throw new InvalidOperationException("Slot is not archived.");
            _states[id]=SessionPhase.Dormant; // never claim Ready after restore
        }
    }
    public IReadOnlyList<SessionSnapshot> Snapshot()
    {
        lock(_gate)
            return _states.Select(x=>new SessionSnapshot(x.Key,x.Value,HasSlot(x.Value))).ToArray();
    }
    public async ValueTask DisposeAsync()
    {
        List<Guid> toClose;
        lock(_gate)
        {
            if(_disposed)return;
            _disposed=true;
            toClose=_states.Where(x=>HasSlot(x.Value)).Select(x=>x.Key).ToList();
        }
        foreach(var id in toClose)
        {
            try
            {
                await _runtime.CloseAsync(id,CancellationToken.None);
                lock(_gate){_states[id]=SessionPhase.Dormant;}
                _connections.Release();
            }
            catch {lock(_gate){_states[id]=SessionPhase.NeedsReview;}}
        }
        // Cannot truthfully assert native handles have closed if any NeedsReview remain.
        // Semaphore disposal is safe once no new calls are admitted.
        _connections.Dispose();
    }
    private void ThrowIfDisposed(){if(_disposed)throw new ObjectDisposedException(nameof(SessionSupervisor));}
}
