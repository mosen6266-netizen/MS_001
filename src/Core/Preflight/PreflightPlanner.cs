using Mosen.GroupScheduler.Groups;
using Mosen.GroupScheduler.Scripts.Editor;
using Mosen.GroupScheduler.Scripts.Portable;
using Mosen.GroupScheduler.Scripts.Preview;

namespace Mosen.GroupScheduler.Preflight;

public sealed record PreflightAccount(Guid AccountId,string Remark,bool LiveAuthorized,MembershipInfo? Membership);
public sealed record PreflightTarget(string ChatId,string? TopicId,GroupInfo? Group,TopicInfo? Topic,
    IReadOnlyList<PreflightAccount> Accounts,bool MembershipsFreshForStart,bool HasConflictingTask);
public sealed record RoleChoice(string ChatId,string? TopicId,string RoleKey,Guid AccountId);
public sealed record PreflightIssue(string ChatId,string? TopicId,string Code,string Message,string? RoleLabel=null);
public sealed record FrozenBinding(string RoleKey,Guid AccountId);
public sealed record FrozenStep(string StepKey,Guid AccountId,string Body,MediaReference? Media,
    int PreSendDelayMs,int TypingDurationMs,int PostSendDelayMs);
public sealed record FrozenTarget(string ChatId,string? TopicId,IReadOnlyList<FrozenBinding> Bindings,
    IReadOnlyList<FrozenStep> Steps);
public sealed record PreflightResult(bool Ready,int Version,string ScriptSha256,
    IReadOnlyList<PreflightIssue> Issues,IReadOnlyList<FrozenTarget> FrozenTargets);
/// <summary>Read-only gate, never queues, logs-in, rotates accounts or transmits messages.</summary>
public static class PreflightPlanner
{
    public static PreflightResult Check(PublishedScript published,IReadOnlyList<PreflightTarget> targets,
        IReadOnlyList<RoleChoice> choices,Func<MediaReference,bool> mediaIsValid,DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(published);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(choices);
        ArgumentNullException.ThrowIfNull(mediaIsValid);
        EditorDocumentRules.Validate(published.Document);
        if(targets.Count is < 1 or > 50)throw new ArgumentException("Select 1 to 50 groups.");
        if(choices.Count>15000)throw new ArgumentException("Too many role selections.");
        var issues=new List<PreflightIssue>();
        var frozen=new List<FrozenTarget>();
        var usedRoles=published.Document.Roles.Where(r=>published.Document.Steps.Any(s=>s.RoleKey==r.RoleKey)).ToArray();
        bool needsImage=published.Document.Steps.Any(s=>s.Media is not null);
        bool needsText=published.Document.Steps.Any(s=>s.Body.Length>0);
        var imageState=new Dictionary<string,bool>(StringComparer.Ordinal);
        foreach(var m in published.Document.Steps.Where(s=>s.Media is not null).Select(s=>s.Media!))
            if(!imageState.ContainsKey(m.AssetId))
            {
                bool good=false;
                try { good=mediaIsValid(m); }catch(Exception) {good=false;}
                imageState.Add(m.AssetId,good);
            }
        var targetKeys=new HashSet<string>(StringComparer.Ordinal);
        foreach(var target in targets)
        {
            var groupId=target.ChatId;
            var topic=target.TopicId;
            void Issue(string code,string message,string? role=null)=>issues.Add(new(groupId,topic,code,message,role));
            string key=groupId+"|"+(topic??"");
            if(!targetKeys.Add(key)){Issue("DUPLICATE_TARGET","同一个群或话题重复选择。");continue;}
            if(target.HasConflictingTask)Issue("TASK_CONFLICT","此群/话题已有未结束任务。");
            if(target.Group is null||target.Group.ChatId!=groupId||target.Group.Kind is not ("group" or "supergroup"))
                Issue("INVALID_GROUP","目标群尚未经过可信 Telegram 群组验证。");
            if(target.Group?.IsForum==true&&topic is null)
                Issue("TOPIC_REQUIRED","论坛群需要指定有效话题。");
            if(topic is not null&&(target.Topic is null||target.Topic.TopicId!=topic||
               target.Topic.ChatId!=groupId||target.Topic.IsClosed||target.Topic.IsHidden))
                Issue("TOPIC_UNAVAILABLE","指定话题不存在或已关闭。");
            if(target.Accounts.Count>300)throw new ArgumentException("Too many accounts for a group.");
            foreach(var m in published.Document.Steps.Where(s=>s.Media is not null).Select(s=>s.Media!))
                if(!imageState[m.AssetId])Issue("MEDIA_INVALID","图片不存在、已损坏或元数据不匹配："+m.FileName);
            var binding=new List<FrozenBinding>();
            foreach(var role in usedRoles)
            {
                var exact=target.Accounts.Where(a=>RoleRemarkResolver.Normalize(a.Remark)==
                     RoleRemarkResolver.Normalize(role.RoleLabel)).ToArray();
                if(exact.Length==0)
                {Issue("ROLE_MISSING","缺少发言备注："+role.RoleLabel,role.RoleLabel);continue;}
                var permitted=new List<PreflightAccount>();
                foreach(var account in exact)
                {
                    if(!account.LiveAuthorized)continue;
                    if(account.Membership?.AccountId!=account.AccountId)continue;
                    var p=GroupPermissionPolicy.Preflight(target.Group,target.Topic,account.Membership,
                        needsImage,now,target.MembershipsFreshForStart);
                    if(p.Allowed && (!needsText||account.Membership!.CanSendText))permitted.Add(account);
                }
                if(permitted.Count==0)
                {Issue("ACCOUNT_UNAVAILABLE","账号备注“"+role.RoleLabel+"”未通过实时授权、群权限或慢速模式预检。",role.RoleLabel);continue;}
                var selection=choices.Where(x=>x.ChatId==groupId&&x.TopicId==topic&&x.RoleKey==role.RoleKey).ToArray();
                if(selection.Length>1)
                {Issue("DUPLICATE_CHOICE","同一个角色有多个显式选择。",role.RoleLabel);continue;}
                PreflightAccount? selected;
                if(selection.Length==1)
                {
                    selected=permitted.FirstOrDefault(a=>a.AccountId==selection[0].AccountId);
                    if(selected is null)
                    {Issue("CHOICE_INVALID","指定账号已失效或没有权限："+role.RoleLabel,role.RoleLabel);continue;}
                }
                else if(permitted.Count==1)selected=permitted[0];
                else {Issue("ROLE_AMBIGUOUS","相同备注有多个合格账号，需要手动指定："+role.RoleLabel,role.RoleLabel);continue;}
                binding.Add(new(role.RoleKey,selected.AccountId));
            }
            if(issues.Any(x=>x.ChatId==groupId&&x.TopicId==topic))continue;
            var assignments=binding.ToDictionary(x=>x.RoleKey,x=>x.AccountId);
            var steps=published.Document.Steps.Select(s=>new FrozenStep(s.StepKey,assignments[s.RoleKey],
                s.Body,s.Media is null?null:s.Media with {},
                s.PreSendDelayMs,s.TypingDurationMs,s.PostSendDelayMs)).ToArray();
            frozen.Add(new(groupId,topic,binding.ToArray(),steps));
        }
        return new(issues.Count==0,published.Version,published.Sha256,issues.ToArray(),
            issues.Count==0?frozen.ToArray():Array.Empty<FrozenTarget>());
    }
}
