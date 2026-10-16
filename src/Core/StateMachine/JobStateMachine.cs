namespace Mosen.GroupScheduler.StateMachine;

/// <summary>Pure virtual-clock transitions: no message send is implemented.</summary>
public static class JobStateMachine
{
    public static SchedulerJob Begin(SchedulerJob job,DateTimeOffset now)
    {
        if(job.Status!=JobStatus.Ready||job.Steps.Count==0)throw new InvalidOperationException("Job cannot start.");
        return job with {Status=JobStatus.Running,Phase=JobPhase.WaitingBefore,
            RemainingMs=job.Steps[0].PreSendDelayMs,LastClockUtc=now};
    }
    /// <summary>At most one non-zero-duration phase can finish per clock tick; waiting and typing never overlap.</summary>
    public static SchedulerJob Advance(SchedulerJob job,DateTimeOffset now)
    {
        if(job.Status!=JobStatus.Running)return job;
        if(now<job.LastClockUtc)throw new ArgumentOutOfRangeException(nameof(now),"Virtual clock moved backwards.");
        if(job.Phase is JobPhase.AwaitingSend or JobPhase.AwaitingReceipt)return job with {LastClockUtc=now};
        if(job.NextStepIndex>=job.Steps.Count)throw new InvalidOperationException("Invalid step cursor.");
        long elapsed=Math.Max(0,(long)Math.Floor((now-job.LastClockUtc).TotalMilliseconds));
        long remaining=Math.Max(0,job.RemainingMs-elapsed);
        var current=job.Steps[job.NextStepIndex];
        var next=job with {RemainingMs=remaining,LastClockUtc=now};
        if(remaining!=0)return next;
        // Zero-duration phases may be skipped without sending. A non-zero phase is never overlapped.
        if(job.Phase==JobPhase.WaitingBefore)
        {
            if(current.TypingDurationMs==0)
                return next with {Phase=JobPhase.AwaitingSend,RemainingMs=0};
            return next with {Phase=JobPhase.Typing,RemainingMs=current.TypingDurationMs};
        }
        if(job.Phase==JobPhase.Typing)return next with {Phase=JobPhase.AwaitingSend,RemainingMs=0};
        if(job.Phase==JobPhase.WaitingAfter)
        {
            var index=job.NextStepIndex+1;
            if(index==job.Steps.Count)
                return next with {Status=JobStatus.Completed,Phase=JobPhase.Finished,
                    NextStepIndex=index,RemainingMs=0};
            return next with {Phase=JobPhase.WaitingBefore,NextStepIndex=index,
                RemainingMs=job.Steps[index].PreSendDelayMs};
        }
        throw new InvalidOperationException("Unsupported clock transition.");
    }
    public static SchedulerJob MarkSubmitted(SchedulerJob job,DateTimeOffset now)
    {
        if(job.Status!=JobStatus.Running||job.Phase!=JobPhase.AwaitingSend)
            throw new InvalidOperationException("Not waiting for an externally approved send.");
        return job with {Phase=JobPhase.AwaitingReceipt,LastClockUtc=now};
    }
    public static SchedulerJob MarkConfirmed(SchedulerJob job,DateTimeOffset now)
    {
        if(job.Status!=JobStatus.Running||job.Phase!=JobPhase.AwaitingReceipt)
            throw new InvalidOperationException("No pending externally confirmed receipt.");
        return job with {Phase=JobPhase.WaitingAfter,
            RemainingMs=job.Steps[job.NextStepIndex].PostSendDelayMs,LastClockUtc=now};
    }
    public static SchedulerJob Pause(SchedulerJob job,DateTimeOffset now)
    {
        if(job.Status!=JobStatus.Running)throw new InvalidOperationException("Only running jobs can pause.");
        var current=Advance(job,now);
        return current.Status==JobStatus.Completed?current:current with {Status=JobStatus.Paused,LastClockUtc=now};
    }
    public static SchedulerJob Resume(SchedulerJob job,DateTimeOffset now)
    {
        if(job.Status!=JobStatus.Paused)throw new InvalidOperationException("Cannot resume a non-paused job.");
        return job with {Status=JobStatus.Running,LastClockUtc=now};
    }
    public static SchedulerJob Review(SchedulerJob job,DateTimeOffset now)
    {
        if(job.Terminal)return job;
        return job with {Status=JobStatus.Review,LastClockUtc=now};
    }
    public static SchedulerJob Stop(SchedulerJob job,DateTimeOffset now)
    {
        if(job.Terminal)return job;
        return job with {Status=JobStatus.Stopped,Phase=JobPhase.Finished,RemainingMs=0,LastClockUtc=now};
    }
    public static SchedulerJob Fail(SchedulerJob job,DateTimeOffset now)
    {
        if(job.Terminal)return job;
        return job with {Status=JobStatus.Failed,Phase=JobPhase.Finished,RemainingMs=0,LastClockUtc=now};
    }
}
