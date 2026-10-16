using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mosen.GroupScheduler.Scripts.Editor;
public sealed record ScriptRole(string RoleKey,string RoleLabel);
public sealed record TimingProfile(int PreSendDelayMs=5000,int TypingDurationMs=5000,int PostSendDelayMs=0)
{
    public static readonly TimingProfile Standard=new();
    public void Validate()
    {
        if(PreSendDelayMs is < 0 or > 3600000 || TypingDurationMs is < 0 or > 60000 ||
           PostSendDelayMs is < 0 or > 3600000 || PreSendDelayMs % 1000 != 0 ||
           TypingDurationMs % 1000 != 0 || PostSendDelayMs % 1000 != 0)
            throw new ArgumentException("Timing values must be whole seconds within allowed ranges.");
    }
}
public sealed record MediaReference(string AssetId,string MimeType,long ByteLength,int Width,int Height,string FileName);
public sealed record EditorStep(string StepKey,string RoleKey,string Body,
    int PreSendDelayMs=5000,int TypingDurationMs=5000,int PostSendDelayMs=0,MediaReference? Media=null);
public sealed record EditorDocument(string Title,List<ScriptRole> Roles,List<EditorStep> Steps,
    TimingProfile? Defaults=null);
public sealed record DraftSnapshot(string ScriptId,long Revision,EditorDocument Document,bool Archived);
public sealed record DraftSummary(string ScriptId,string Title,long Revision,int StepCount,bool Archived);
public sealed class DraftConflictException:Exception
{
    public DraftConflictException():base("Draft revision conflict."){}
}
/// <summary>No local IDs, phone, sessions or unknown properties may cross the script draft boundary.</summary>
public static class EditorDocumentRules
{
    public static readonly JsonSerializerOptions WireOptions=new(JsonSerializerDefaults.Web){
        UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,
        MaxDepth=12
    };
    public static EditorDocument Parse(JsonElement raw)
    {
        if(raw.ValueKind!=JsonValueKind.Object || raw.GetRawText().Length>524288)
            throw new ArgumentException("Draft exceeds permitted size.");
        EditorDocument? doc=raw.Deserialize<EditorDocument>(WireOptions);
        return Validate(doc??throw new ArgumentException("Invalid draft document."));
    }
    public static EditorDocument Validate(EditorDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        if(doc.Title is null||doc.Title.Length is <1 or >128||string.IsNullOrWhiteSpace(doc.Title)||
           doc.Title.Any(ch=>ch=='\0'))throw new ArgumentException("Title is invalid.");
        if(doc.Roles is null||doc.Steps is null||doc.Roles.Count>300||doc.Steps.Count>1000)
            throw new ArgumentException("Draft limits exceeded.");
        (doc.Defaults??TimingProfile.Standard).Validate();
        var keys=new HashSet<string>(StringComparer.Ordinal);
        var aliases=new HashSet<string>(StringComparer.Ordinal);
        foreach(var role in doc.Roles)
        {
            if(!Guid.TryParseExact(role.RoleKey,"D",out var id)||id==Guid.Empty ||
                role.RoleLabel is null||role.RoleLabel.Length is <1 or >64||
                string.IsNullOrWhiteSpace(role.RoleLabel)||role.RoleLabel.Any(char.IsControl))
                throw new ArgumentException("Invalid role metadata.");
            if(!keys.Add(role.RoleKey))throw new ArgumentException("Duplicate role key.");
            if(!aliases.Add(role.RoleLabel.Trim().Normalize(System.Text.NormalizationForm.FormC)))
                throw new ArgumentException("Duplicate role label; choose explicit role mapping.");
        }
        var stepKeys=new HashSet<string>(StringComparer.Ordinal);
        foreach(var step in doc.Steps)
        {
            if(!Guid.TryParseExact(step.StepKey,"D",out var id)||id==Guid.Empty||
               !stepKeys.Add(step.StepKey)||!keys.Contains(step.RoleKey) ||
               step.Body is null||step.Body.Length>8192||
               step.PreSendDelayMs is <0 or >3600000 ||
               step.TypingDurationMs is <0 or >60000 ||
               step.PostSendDelayMs is <0 or >3600000 ||
               step.PreSendDelayMs % 1000 != 0 || step.TypingDurationMs % 1000 != 0 ||
               step.PostSendDelayMs % 1000 != 0)
                throw new ArgumentException("Invalid script step; all steps require a selected role.");
            if(step.Media is not null)
            {
                var m=step.Media;
                if(m.AssetId is null || m.AssetId.Length!=64 ||
                   m.AssetId.Any(x=>x is not (>= '0' and <= '9' or >= 'a' and <= 'f')) ||
                   m.MimeType is not ("image/png" or "image/jpeg" or "image/webp") ||
                   m.ByteLength is < 32 or > 5242880 ||
                   m.Width is < 1 or > 8192 || m.Height is < 1 or > 8192 ||
                   m.FileName is null || m.FileName.Length is < 1 or > 128 ||
                   m.FileName.Any(x=>x is '/' or '\\' or ':' or '\0' || char.IsControl(x)))
                    throw new ArgumentException("Invalid portable image reference.");
            }
        }
        return doc;
    }
}
