namespace Mosen.GroupScheduler.StateMachine;

/// <summary>Simulation state only. No sender or Telegram service is referenced here.</summary>
public enum JobStatus { Ready, Running, Paused, Review, Completed, Stopped, Failed }
public enum JobPhase { WaitingBefore, Typing, AwaitingSend, AwaitingReceipt, WaitingAfter, Finished }

public sealed record PlannedStep(string StepKey,Guid AccountId,
    int PreSendDelayMs,int TypingDurationMs,int PostSendDelayMs);

/// <summary>Durable state contains only IDs/timing. Message content stays in immutable published versions.</summary>
public sealed record SchedulerJob(string RunId,string ChatId,string? TopicId,string ScriptId,int ScriptVersion,
    string ScriptSha256,List<PlannedStep> Steps,JobStatus Status,JobPhase Phase,
    int NextStepIndex,long RemainingMs,DateTimeOffset LastClockUtc,int Revision)
{
    public int TotalSteps => Steps.Count;
    public bool Terminal => Status is JobStatus.Completed or JobStatus.Stopped or JobStatus.Failed;
}
public sealed record SchedulerNotice(string RunId,string Code,string Message,int NextStepIndex);
