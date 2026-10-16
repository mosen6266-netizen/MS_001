using System.Text.RegularExpressions;
using Mosen.GroupScheduler.Scripts.Editor;
using Mosen.GroupScheduler.Scripts.Timing;

namespace Mosen.GroupScheduler.Scripts.Preview;
public sealed record LinkEntity(int Start,int Length,string Url);
public sealed record PreviewEntry(int Ordinal,string StepKey,string RoleKey,string RoleLabel,string Body,
    string? ImageName,long WaitingMs,long TypingMs,long PostSendMs,long EarliestSendFromStepStartMs,
    IReadOnlyList<LinkEntity> Links);
public sealed record ScriptPreviewReport(int Version,string Sha256,IReadOnlyList<PreviewEntry> Entries,long MinimumDurationWithoutNetworkMs);
public static partial class ScriptPreviewBuilder
{
    [GeneratedRegex(@"https?://[^\s<>\u0000-\u001f]+",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)]
    private static partial Regex UrlPattern();
    public static ScriptPreviewReport Build(PublishedScript published)
    {
        ArgumentNullException.ThrowIfNull(published);
        var doc=EditorDocumentRules.Validate(published.Document);
        var labels=doc.Roles.ToDictionary(x=>x.RoleKey,x=>x.RoleLabel,StringComparer.Ordinal);
        var entries=new List<PreviewEntry>();
        long minimum=0;
        for(int i=0;i<doc.Steps.Count;i++)
        {
            var step=doc.Steps[i];
            var timing=TimingPreviewCalculator.Create(new(step.PreSendDelayMs,step.TypingDurationMs,step.PostSendDelayMs));
            var entities=new List<LinkEntity>();
            foreach(Match match in UrlPattern().Matches(step.Body).Cast<Match>().Take(65))
            {
                var text=match.Value.TrimEnd('.',',',')',';', '！','。');
                if(entities.Count>=64)throw new InvalidDataException("Too many links.");
                if(Uri.TryCreate(text,UriKind.Absolute,out var uri)&&uri.Scheme is "https" or "http" &&
                   uri.Host.Length>0 && uri.UserInfo.Length==0)
                    entities.Add(new(match.Index,text.Length,text));
                else throw new InvalidDataException("Invalid URL in published script.");
            }
            entries.Add(new(i+1,step.StepKey,step.RoleKey,labels[step.RoleKey],step.Body,step.Media?.FileName,
                step.PreSendDelayMs,step.TypingDurationMs,step.PostSendDelayMs,timing.ReadyToSendAtMs,entities));
            minimum=checked(minimum+step.PreSendDelayMs+step.TypingDurationMs+step.PostSendDelayMs);
        }
        return new(published.Version,published.Sha256,entries,minimum);
    }
}
