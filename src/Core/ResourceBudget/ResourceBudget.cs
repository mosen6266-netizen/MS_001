using System.Diagnostics;
namespace Mosen.GroupScheduler.ResourceBudget;

/// <summary>Only aggregate process counters, never customer data or arbitrary exception text.</summary>
public enum PressureLevel { Normal, Conserve, Critical }
public enum BudgetSignal { None, Cpu, WorkingSet, ManagedHeap, Threads, Handles, Timers, Cache, Telemetry, Multiple }
public sealed record ResourceSample(DateTimeOffset CapturedUtc,double CpuPercent,long WorkingSetMiB,
    long ManagedHeapMiB,int ThreadCount,int HandleCount,bool HandlesKnown,
    int TimerSubscriptions,int CacheEntries,long DiagnosticBytes);
public sealed record ResourceLimits(double CpuWarningPercent=70,double CpuCriticalPercent=95,
    long WorkingSetWarningMiB=500,long WorkingSetCriticalMiB=850,
    long HeapWarningMiB=220,long HeapCriticalMiB=440,
    int ThreadWarning=80,int ThreadCritical=150,
    int HandleWarning=1200,int HandleCritical=2400,
    int TimerWarning=4,int TimerCritical=10,
    int CacheWarning=768,int CacheCritical=1024,
    long DiagnosticWarningBytes=384*1024,long DiagnosticCriticalBytes=512*1024);
public sealed record ResourceDecision(PressureLevel Level,BudgetSignal Signal,bool Changed,
    bool ShouldNotify,TimeSpan UiReadInterval,TimeSpan AlertScanInterval,bool AllowOptionalWork,
    int ObservedSamples,DateTimeOffset CheckedUtc);
public static class BudgetValidation
{
    public static void Validate(ResourceLimits l)
    {
        ArgumentNullException.ThrowIfNull(l);
        if(!double.IsFinite(l.CpuWarningPercent)||!double.IsFinite(l.CpuCriticalPercent)||
            l.CpuWarningPercent<=0||l.CpuCriticalPercent>100||
            l.CpuCriticalPercent<=l.CpuWarningPercent)
            throw new ArgumentOutOfRangeException(nameof(l));
        static bool Valid(long warning,long critical)=>warning>0&&critical>warning;
        if(!Valid(l.WorkingSetWarningMiB,l.WorkingSetCriticalMiB)||
           !Valid(l.HeapWarningMiB,l.HeapCriticalMiB)||
           !Valid(l.ThreadWarning,l.ThreadCritical)||
           !Valid(l.HandleWarning,l.HandleCritical)||
           !Valid(l.TimerWarning,l.TimerCritical)||
           !Valid(l.CacheWarning,l.CacheCritical)||
           !Valid(l.DiagnosticWarningBytes,l.DiagnosticCriticalBytes))
            throw new ArgumentOutOfRangeException(nameof(l));
    }
}

/// <summary>Conservative hysteresis: two consecutive high samples degrade; three low
/// samples recover; alerts edge-triggered, with 30-minute bounded reminders.</summary>
public sealed class ResourceBudgetMonitor
{
    private readonly ResourceLimits _limits;
    private PressureLevel _level;
    private BudgetSignal _signal;
    private int _high;
    private int _low;
    private int _samples;
    private DateTimeOffset? _lastSample;
    private DateTimeOffset? _lastWarning;
    private readonly object _gate=new();
    public ResourceBudgetMonitor(ResourceLimits? limits=null)
    {
        _limits=limits??new();BudgetValidation.Validate(_limits);
    }
    private static void Check(ResourceSample s)
    {
        if(!double.IsFinite(s.CpuPercent)||s.CpuPercent<0||s.CpuPercent>100||
           s.WorkingSetMiB<0||s.ManagedHeapMiB<0||s.ThreadCount<0||
           s.HandleCount<0||s.TimerSubscriptions<0||s.CacheEntries<0||s.DiagnosticBytes<0)
            throw new ArgumentOutOfRangeException(nameof(s));
    }
    private (PressureLevel level,BudgetSignal signal) Classify(ResourceSample s)
    {
        var warning=new List<BudgetSignal>(),critical=new List<BudgetSignal>();
        void Add(bool w,bool c,BudgetSignal signal){if(c)critical.Add(signal);else if(w)warning.Add(signal);}
        Add(s.CpuPercent>=_limits.CpuWarningPercent,s.CpuPercent>=_limits.CpuCriticalPercent,BudgetSignal.Cpu);
        Add(s.WorkingSetMiB>=_limits.WorkingSetWarningMiB,s.WorkingSetMiB>=_limits.WorkingSetCriticalMiB,BudgetSignal.WorkingSet);
        Add(s.ManagedHeapMiB>=_limits.HeapWarningMiB,s.ManagedHeapMiB>=_limits.HeapCriticalMiB,BudgetSignal.ManagedHeap);
        Add(s.ThreadCount>=_limits.ThreadWarning,s.ThreadCount>=_limits.ThreadCritical,BudgetSignal.Threads);
        if(s.HandlesKnown)Add(s.HandleCount>=_limits.HandleWarning,s.HandleCount>=_limits.HandleCritical,BudgetSignal.Handles);
        Add(s.TimerSubscriptions>=_limits.TimerWarning,s.TimerSubscriptions>=_limits.TimerCritical,BudgetSignal.Timers);
        Add(s.CacheEntries>=_limits.CacheWarning,s.CacheEntries>=_limits.CacheCritical,BudgetSignal.Cache);
        Add(s.DiagnosticBytes>=_limits.DiagnosticWarningBytes,s.DiagnosticBytes>=_limits.DiagnosticCriticalBytes,BudgetSignal.Telemetry);
        var relevant=critical.Count>0?critical:warning;
        return (critical.Count>0?PressureLevel.Critical:
            warning.Count>0?PressureLevel.Conserve:PressureLevel.Normal,
            relevant.Count>1?BudgetSignal.Multiple:
            relevant.Count==1?relevant[0]:BudgetSignal.None);
    }
    public ResourceDecision Observe(ResourceSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);Check(sample);
        lock(_gate)
        {
            if(_lastSample is not null&&sample.CapturedUtc<_lastSample.Value)
                throw new ArgumentOutOfRangeException(nameof(sample),"Clock regressed.");
            _lastSample=sample.CapturedUtc;
            _samples++;
            var (level,signal)=Classify(sample);
            bool changed=false;
            if(level>_level)
            {
                _high++;_low=0;
                if(_high>=2){_level=level;_signal=signal;changed=true;_high=0;}
            }
            else if(level<_level)
            {
                _low++;_high=0;
                if(_low>=3){_level=level;_signal=level==PressureLevel.Normal?BudgetSignal.None:signal;
                    _low=0;changed=true;}
            }
            else{_high=0;_low=0;if(_level!=PressureLevel.Normal)_signal=signal;}
            bool notify=_level!=PressureLevel.Normal&&
                (changed||_lastWarning is null||
                    sample.CapturedUtc-_lastWarning.Value>=TimeSpan.FromMinutes(30));
            if(notify)_lastWarning=sample.CapturedUtc;
            return new(_level,_signal,changed,notify,
                _level switch{PressureLevel.Normal=>TimeSpan.FromSeconds(5),
                    PressureLevel.Conserve=>TimeSpan.FromSeconds(15),
                    _=>TimeSpan.FromSeconds(30)},
                _level switch{PressureLevel.Normal=>TimeSpan.FromSeconds(6),
                    PressureLevel.Conserve=>TimeSpan.FromSeconds(30),
                    _=>TimeSpan.FromSeconds(60)},
                _level==PressureLevel.Normal,_samples,sample.CapturedUtc);
        }
    }
}
/// <summary>Process-wide no-sensitive-data sampler with monotonic CPU deltas.
/// First sample has unknown CPU usage and reports zero, not a synthetic burst.</summary>
public sealed class ProcessResourceSampler
{
    private readonly object _lock=new();
    private TimeSpan? _lastCpu;
    private DateTimeOffset? _lastUtc;
    public ResourceSample Sample(DateTimeOffset now,int timers,int cacheEntries,long diagnosticBytes)
    {
        lock(_lock)
        {
            using var process=Process.GetCurrentProcess();
            var cpu=process.TotalProcessorTime;
            double load=0;
            if(_lastCpu is not null&&_lastUtc is not null&&now>_lastUtc.Value)
            {
                var elapsed=(now-_lastUtc.Value).TotalMilliseconds;
                load=Math.Clamp((cpu-_lastCpu.Value).TotalMilliseconds/
                    (elapsed*Math.Max(1,Environment.ProcessorCount))*100,0,100);
            }
            _lastCpu=cpu;_lastUtc=now;
            int handles=0;bool known=false;
            try{if(OperatingSystem.IsWindows()){handles=process.HandleCount;known=true;}}
            catch(Exception e) when(e is InvalidOperationException or
                 System.ComponentModel.Win32Exception){known=false;}
            return new(now,load,Math.Max(0,process.WorkingSet64/(1024*1024)),
                Math.Max(0,GC.GetTotalMemory(false)/(1024*1024)),
                Math.Max(0,process.Threads.Count),Math.Max(0,handles),known,
                timers,cacheEntries,diagnosticBytes);
        }
    }
}
