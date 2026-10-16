using Mosen.GroupScheduler.OutboundPolicy;
using Mosen.GroupScheduler.Scheduler;

namespace Mosen.GroupScheduler.Notifications;
public sealed record EmergencyStopResult(bool PermitGateClosed,bool PersistentLatchWritten,
    bool AllSimulationsStopped,bool AlreadyStopped,string Code);
/// <summary>Immediate outbound barrier; atomic persistent stop latch; best-effort durable simulation shutdown.
/// No real TDLib sender currently exists. Future sender MUST consult the latched stop before ANY send.</summary>
public sealed class EmergencyStopController
{
    private readonly object _guard=new();
    private readonly OutboundCoordinator _policy;
    private readonly Func<DateTimeOffset,FairScheduler?> _schedulerFactory;
    private readonly string _latchPath;
    private bool _closed;
    private bool _durableJobsStopped;
    public EmergencyStopController(OutboundCoordinator policy,string latchPath,
        Func<DateTimeOffset,FairScheduler?> schedulerFactory)
    {
        _policy=policy??throw new ArgumentNullException(nameof(policy));
        if(string.IsNullOrWhiteSpace(latchPath))throw new ArgumentException("Missing latch path.");
        _latchPath=Path.GetFullPath(latchPath);
        _schedulerFactory=schedulerFactory??throw new ArgumentNullException(nameof(schedulerFactory));
        // A malformed or unreadable existing latch must fail closed.
        if(File.Exists(_latchPath))_closed=true;
        if(_closed)_policy.Shutdown();
    }
    public bool IsStopped=>_closed;
    public EmergencyStopResult Stop(DateTimeOffset now)
    {
        lock(_guard)
        {
            _policy.Shutdown(); // first action; even if disk I/O subsequently throws
            if(_closed && _durableJobsStopped)return new(true,File.Exists(_latchPath),true,true,"ALREADY_STOPPED");
            _closed=true; // never reopen the permit gate, including after I/O failure
            bool persisted=File.Exists(_latchPath),simulationsStopped=false;
            try
            {
                var directory=Path.GetDirectoryName(_latchPath)!;
                Directory.CreateDirectory(directory);
                var temp=_latchPath+"."+Guid.NewGuid().ToString("N")+".tmp";
                try
                {
                    File.WriteAllText(temp,"EMERGENCY_STOPPED_V1\n");
                    File.Move(temp,_latchPath,overwrite:true);
                    persisted=true;
                }
                finally {if(File.Exists(temp))File.Delete(temp);}
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException)
            {
                return new(true,false,false,false,"LATCH_WRITE_FAILED");
            }
            try
            {
                var scheduler=_schedulerFactory(now);
                if(scheduler is not null)
                {
                    scheduler.StopAll(now); // one SQLite transaction for every nonterminal mock task
                    simulationsStopped=true;
                }
                else simulationsStopped=true;
                _durableJobsStopped=true;
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or
                InvalidOperationException or Microsoft.Data.Sqlite.SqliteException or ArgumentException)
            {
                return new(true,persisted,false,false,"SIMULATION_STOP_REVIEW");
            }
            return new(true,persisted,simulationsStopped,false,"STOPPED");
        }
    }
}
