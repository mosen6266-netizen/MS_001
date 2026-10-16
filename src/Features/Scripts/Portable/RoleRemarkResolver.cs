using System.Text;
using Mosen.GroupScheduler.Scripts.Editor;

namespace Mosen.GroupScheduler.Scripts.Portable;

public sealed record LocalRemarkCandidate(string LocalAccountId,string Remark);
public sealed record RoleRemarkResult(string RoleKey,string RoleLabel,string Status,IReadOnlyList<string> CandidateIds);
/// <summary>Exact normalized remark lookup; never picks an arbitrary person from duplicate remarks.</summary>
public static class RoleRemarkResolver
{
    public static string Normalize(string? value)=>(value??"").Trim().Normalize(NormalizationForm.FormC);
    public static IReadOnlyList<RoleRemarkResult> Resolve(EditorDocument script,
        IEnumerable<LocalRemarkCandidate> accounts,IReadOnlySet<string>? eligibleInGroup=null)
    {
        EditorDocumentRules.Validate(script);
        ArgumentNullException.ThrowIfNull(accounts);
        var byRemark=accounts
            .Where(x=>!string.IsNullOrWhiteSpace(x.LocalAccountId))
            .GroupBy(x=>Normalize(x.Remark),StringComparer.Ordinal)
            .ToDictionary(x=>x.Key,x=>x.Select(y=>y.LocalAccountId).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        return script.Roles
            .Where(role=>script.Steps.Any(step=>step.RoleKey==role.RoleKey))
            .Select(role=>{
                var ids=byRemark.TryGetValue(Normalize(role.RoleLabel),out var matched)
                    ?matched:Array.Empty<string>();
                if(eligibleInGroup is not null)ids=ids.Where(eligibleInGroup.Contains).ToArray();
                var status=ids.Length switch {0=>"missing",1=>"unique",_=>"ambiguous"};
                return new RoleRemarkResult(role.RoleKey,role.RoleLabel,status,ids);
            }).ToArray();
    }
}