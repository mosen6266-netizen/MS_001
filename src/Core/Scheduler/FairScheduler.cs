using Mosen.GroupScheduler.Preflight;
using Mosen.GroupScheduler.StateMachine;

namespace Mosen.GroupScheduler.Scheduler;

/// <summary>Manual deterministic simulator. No timers, no networking, no TDLib calls, no background sends.</summary>
public sealed class FairScheduler
{
    private readonly SchedulerCheckpointStore _store;
    private readonly Dictionary<string,SchedulerJob> _jobs=new(StringComparer.Ordinal);
    private int _cursor;
    private bool _shutdown;
    public bool IsShutdown=>_shutdown;
    public FairScheduler(SchedulerCheckpointStore store,DateTimeOffset now)
    {
        _store=store??throw new ArgumentNullException(nameof(store));
        foreach(var j in _store.LoadFailClosed(now))_jobs.Add(j.RunId,j);
    }
    public IReadOnlyList<SchedulerJob> Snapshot()=>_jobs.Values.OrderBy(j=>j.ChatId,StringComparer.Ordinal)
        .ThenBy(j=>j.TopicId,StringComparer.Ordinal)
        .Select(j=>j with {Steps=new List<PlannedStep>(j.Steps)}).ToArray();
    public IReadOnlyList<SchedulerJob> CreateSimulations(Guid scriptId,PreflightResult checkedPlan,DateTimeOffset now)
    {
        EnsureOpen();
        if(scriptId==Guid.Empty||checkedPlan is null||!checkedPlan.Ready||
           checkedPlan.Issues.Count!=0||checkedPlan.FrozenTargets.Count is <1 or >50 ||
           checkedPlan.Version<1||checkedPlan.ScriptSha256 is not {Length:64})
            throw new InvalidOperationException("Only a successful complete preflight can seed simulations.");
        var targetKeys=new HashSet<string>(StringComparer.Ordinal);
        var jobs=new List<SchedulerJob>();
        foreach(var target in checkedPlan.FrozenTargets)
        {
            if(!long.TryParse(target.ChatId,out var chat)||chat==0||
               !targetKeys.Add(target.ChatId+"|"+(target.TopicId??""))||
               target.Steps.Count is <1 or >1000)
                throw new InvalidOperationException("Invalid or duplicate group plan.");
            var steps=target.Steps.Select(s=>new PlannedStep(s.StepKey,s.AccountId,
                s.PreSendDelayMs,s.TypingDurationMs,s.PostSendDelayMs)).ToList();
            var job=new SchedulerJob(Guid.NewGuid().ToString("D"),target.ChatId,target.TopicId,
                scriptId.ToString("D"),checkedPlan.Version,checkedPlan.ScriptSha256,steps,
                JobStatus.Ready,JobPhase.WaitingBefore,0,steps[0].PreSendDelayMs,now,1);
            jobs.Add(job);
        }
        if(_jobs.Count+jobs.Count>50)throw new InvalidOperationException("Simulation group limit reached.");
        _store.CreateBatch(jobs); // A failed batch never leaves half-created runs.
        foreach(var j in jobs)_jobs.Add(j.RunId,j);
        return jobs;
    }
    private void EnsureOpen()
    {if(_shutdown)throw new InvalidOperationException("Scheduler is shut down and cannot restart in this process.");}
    private SchedulerJob Mutate(string id,Func<SchedulerJob,SchedulerJob> change)
    {
        EnsureOpen();
        if(!_jobs.TryGetValue(id,out var original))throw new KeyNotFoundException("Simulation run missing.");
        var transformed=change(original);
        if(transformed==original)return original;
        var revised=transformed with {Revision=original.Revision+1};
        _store.Update(revised,original.Revision);
        _jobs[id]=revised;
        return revised;
    }
    public SchedulerJob Start(string runId,DateTimeOffset now)=>
        Mutate(runId,j=>JobStateMachine.Begin(j,now));
    public SchedulerJob Pause(string runId,DateTimeOffset now)=>
        Mutate(runId,j=>JobStateMachine.Pause(j,now));
    public SchedulerJob Resume(string runId,DateTimeOffset now)=>
        Mutate(runId,j=>JobStateMachine.Resume(j,now));
    public SchedulerJob Stop(string runId,DateTimeOffset now)=>
        Mutate(runId,j=>JobStateMachine.Stop(j,now));
    public SchedulerJob Fail(string runId,DateTimeOffset now)=>
        Mutate(runId,j=>JobStateMachine.Fail(j,now));
    public SchedulerJob ReviewUnknown(string runId,DateTimeOffset now)=>
        Mutate(runId,j=>JobStateMachine.Review(j,now));
    /// <summary>For synthetic tests only; real outbound/receipt policy belongs to Steps18/19.</summary>
    public SchedulerJob SimulateSubmitted(string runId,DateTimeOffset now)=>
        Mutate(runId,j=>JobStateMachine.MarkSubmitted(j,now));
    public SchedulerJob SimulateConfirmed(string runId,DateTimeOffset now)=>
        Mutate(runId,j=>JobStateMachine.MarkConfirmed(j,now));
    public IReadOnlyList<SchedulerNotice> Tick(DateTimeOffset now)
    {
        EnsureOpen();
        var ordered=_jobs.Values.Where(j=>j.Status==JobStatus.Running)
            .OrderBy(j=>j.ChatId,StringComparer.Ordinal).ThenBy(j=>j.TopicId,StringComparer.Ordinal).ToArray();
        if(ordered.Length==0)return Array.Empty<SchedulerNotice>();
        var events=new List<SchedulerNotice>();
        // Rotate the first recipient each virtual tick so long-running groups never starve.
        int offset=_cursor%ordered.Length;_cursor++;
        for(int i=0;i<ordered.Length;i++)
        {
            var id=ordered[(offset+i)%ordered.Length].RunId;
            try
            {
                var after=Mutate(id,j=>JobStateMachine.Advance(j,now));
                if(after.Phase==JobPhase.AwaitingSend)
                    events.Add(new(id,"AWAITING_EXTERNAL_SEND","仅模拟：等待后续步骤的外部安全发送许可。",after.NextStepIndex));
                else if(after.Status==JobStatus.Completed)
                    events.Add(new(id,"COMPLETED","此群的模拟剧本已执行完毕。",after.NextStepIndex));
                else events.Add(new(id,"TICK","模拟时间推进。",after.NextStepIndex));
            }
            catch(Exception)
            {
                // Failing one group's clock/checkpoint must not prevent other groups from progressing.
                try { _=Mutate(id,j=>JobStateMachine.Review(j,now)); }
                catch(Exception) { /* fail closed: keep prior in-memory state, report checkpoint failure */ }
                events.Add(new(id,"CHECKPOINT_FAILED","此群发生模拟错误，需要人工复核。",_jobs[id].NextStepIndex));
            }
        }
        return events;
    }
    /// <summary>Power/network interruption puts every nonterminal simulated run into manual review,
    /// writes all checkpoints atomically, and permanently closes this process's scheduler.</summary>
    public void HoldAllForReview(DateTimeOffset now)
    {
        EnsureOpen();
        var updates=_jobs.Values.Where(x=>!x.Terminal && x.Status!=JobStatus.Review)
            .Select(x=>(Next:JobStateMachine.Review(x,now) with {Revision=x.Revision+1},
                Previous:x.Revision)).ToArray();
        _store.UpdateBatch(updates);
        foreach(var (next,_) in updates)_jobs[next.RunId]=next;
        _shutdown=true;
    }
    public void StopAll(DateTimeOffset now)
    {
        EnsureOpen();
        var changes=_jobs.Values.Where(x=>!x.Terminal)
            .Select(x=>(Next:JobStateMachine.Stop(x,now) with {Revision=x.Revision+1},Previous:x.Revision)).ToArray();
        _store.UpdateBatch(changes); // all-or-nothing persistence
        foreach(var (next,_) in changes)_jobs[next.RunId]=next;
        _shutdown=true;
    }
}
