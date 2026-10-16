using Mosen.GroupScheduler.Delivery;
using Mosen.GroupScheduler.StateMachine;

namespace Mosen.GroupScheduler.RuntimeDashboard;
/// <summary>Read-only projection. All "running" states come from persisted simulation metadata;
/// no real sender, timers, or Telegram connection are part of this model.</summary>
public sealed record RuntimeTaskRow(string RunId,string ChatId,string? TopicId,string State,string Phase,
    int CompletedSteps,int TotalSteps,int CompletionPercent,int NextStepNumber,
    long RemainingMs,DateTimeOffset LastClockUtc,bool IsClockActive,string IssueCode,int Revision);
public sealed record RuntimeSummary(int Total,int Running,int Paused,int Review,int Failed,
    int Completed,int Stopped,int Ready,int UncertainMessages,int CorruptRecords);
public sealed record RuntimeDashboardSnapshot(DateTimeOffset CheckedUtc,string Mode,RuntimeSummary Summary,
    IReadOnlyList<RuntimeTaskRow> Items);
public static class RuntimeDashboardBuilder
{
    public static RuntimeDashboardSnapshot Build(IReadOnlyList<SchedulerJob> jobs,
        IReadOnlyList<DeliveryAttempt> attempts,int corruptRecords,DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(jobs);ArgumentNullException.ThrowIfNull(attempts);
        if(corruptRecords<0||jobs.Count>10000||attempts.Count>100000)
            throw new ArgumentOutOfRangeException(nameof(corruptRecords));
        var issues=attempts.Where(a=>a.State is DeliveryStatus.Unknown or DeliveryStatus.Review)
            .GroupBy(a=>a.RunId,StringComparer.Ordinal)
            .ToDictionary(g=>g.Key,g=>g.Any(a=>a.State==DeliveryStatus.Unknown)
                ?"OUTCOME_UNKNOWN":"DELIVERY_REVIEW",StringComparer.Ordinal);
        var rows=new List<RuntimeTaskRow>(jobs.Count);
        foreach(var j in jobs)
        {
            if(j.TotalSteps<=0||j.NextStepIndex<0||j.NextStepIndex>j.TotalSteps)
                throw new InvalidDataException("Invalid dashboard cursor.");
            int finished=j.Status==JobStatus.Completed?j.TotalSteps:j.NextStepIndex;
            var currentIsConfirmed=attempts.Any(a=>a.RunId==j.RunId&&j.NextStepIndex<j.TotalSteps&&
                a.StepKey==j.Steps[j.NextStepIndex].StepKey&&a.State==DeliveryStatus.Confirmed);
            if(currentIsConfirmed)finished++;
            finished=Math.Clamp(finished,0,j.TotalSteps);
            var issue=issues.TryGetValue(j.RunId,out var deliveryIssue)?deliveryIssue:
                j.Status switch {
                    JobStatus.Review=>"REQUIRES_REVIEW",
                    JobStatus.Failed=>"TASK_FAILED",
                    JobStatus.Paused=>"USER_PAUSED",
                    _=>""
                };
            var active=j.Status==JobStatus.Running &&
                j.Phase is JobPhase.WaitingBefore or JobPhase.Typing or JobPhase.WaitingAfter;
            // Time is computed by UI from one clock; persisted countdown remains unchanged.
            rows.Add(new(j.RunId,j.ChatId,j.TopicId,j.Status.ToString(),j.Phase.ToString(),
                finished,j.TotalSteps,(int)(finished*100L/j.TotalSteps),
                j.Status==JobStatus.Completed?j.TotalSteps:Math.Min(j.NextStepIndex+1,j.TotalSteps),
                j.RemainingMs,j.LastClockUtc,active,issue,j.Revision));
        }
        return new(now,"offline-simulation-readonly",
            new(jobs.Count,jobs.Count(j=>j.Status==JobStatus.Running),
                jobs.Count(j=>j.Status==JobStatus.Paused),
                jobs.Count(j=>j.Status==JobStatus.Review),jobs.Count(j=>j.Status==JobStatus.Failed),
                jobs.Count(j=>j.Status==JobStatus.Completed),jobs.Count(j=>j.Status==JobStatus.Stopped),
                jobs.Count(j=>j.Status==JobStatus.Ready),
                attempts.Count(a=>a.State is DeliveryStatus.Unknown or DeliveryStatus.Review),corruptRecords),rows);
    }
}
