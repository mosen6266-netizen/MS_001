using Mosen.GroupScheduler.Scripts.Editor;
namespace Mosen.GroupScheduler.Scripts.Timing;
public sealed record TimingPhase(string Phase,long BeginMs,long EndMs);
public sealed record TimingPreview(IReadOnlyList<TimingPhase> Phases,long ReadyToSendAtMs,long? FinishedAtMs);
/// <summary>Pure timeline projection. Does not use real clocks or emit any Telegram action.</summary>
public static class TimingPreviewCalculator
{
    public static TimingPreview Create(TimingProfile timing,long? acknowledgedAtMs=null)
    {
        timing.Validate();
        long pre=timing.PreSendDelayMs;
        long typing=timing.TypingDurationMs;
        long due=checked(pre+typing);
        var phases=new List<TimingPhase>{
            new("pre-send-wait",0,pre),
            new("typing-preview",pre,due),
            new("ready-to-send",due,due)
        };
        if(acknowledgedAtMs is long confirmed)
        {
            if(confirmed<due)throw new ArgumentOutOfRangeException(nameof(acknowledgedAtMs));
            phases.Add(new("post-send-wait",confirmed,checked(confirmed+timing.PostSendDelayMs)));
            return new(phases,due,checked(confirmed+timing.PostSendDelayMs));
        }
        return new(phases,due,null);
    }
}
