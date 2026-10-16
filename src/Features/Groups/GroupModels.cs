namespace Mosen.GroupScheduler.Groups;

/// <summary>Chat IDs are stable Telegram identifiers; titles are presentation, never join keys.</summary>
public sealed record GroupInfo(string ChatId,string Title,string Kind,bool IsForum,DateTimeOffset CheckedUtc);
public sealed record TopicInfo(string ChatId,string TopicId,string Title,bool IsClosed,bool IsHidden,DateTimeOffset CheckedUtc);
public sealed record MembershipInfo(
    Guid AccountId,string ChatId,string Status,bool CanSendText,bool CanSendPhotos,
    int SlowModeDelaySeconds,DateTimeOffset CheckedUtc);
public sealed record GroupPage(IReadOnlyList<GroupInfo> Items,int Total,int Page,int PageSize);
public sealed record TopicPage(IReadOnlyList<TopicInfo> Items,int Total,int Page,int PageSize);
public sealed record MembershipDisplay(string AccountId,string Alias,string Status,
    bool CanSendText,bool CanSendPhotos,int SlowModeDelaySeconds,DateTimeOffset CheckedUtc);
public sealed record PermissionDecision(bool Allowed,string Code,string Message,int SlowModeSeconds);

public static class GroupPermissionPolicy
{
    public static PermissionDecision Preflight(GroupInfo? group,TopicInfo? topic,MembershipInfo? membership,
        bool needsPhoto,DateTimeOffset now,bool wasRefreshedForThisStart)
    {
        PermissionDecision Deny(string code,string text) => new(false,code,text,0);
        if(group is null || group.Kind is not ("group" or "supergroup"))
            return Deny("INVALID_TARGET","不是已确认的 Telegram 群组，不能启动。");
        if(topic is not null && (topic.ChatId!=group.ChatId || topic.IsClosed || topic.IsHidden))
            return Deny("TOPIC_UNAVAILABLE","话题已关闭、隐藏或与群组不匹配。");
        if(membership is null || membership.ChatId!=group.ChatId)
            return Deny("MEMBERSHIP_UNKNOWN","账号在此群的成员资格尚未验证。");
        if(!wasRefreshedForThisStart || now < membership.CheckedUtc ||
           now-membership.CheckedUtc > TimeSpan.FromSeconds(60))
            return Deny("RECHECK_REQUIRED","启动前必须向 Telegram 重新检查账号成员身份及发言权限。");
        if(membership.Status is not ("member" or "administrator" or "creator" or "restricted"))
            return Deny("NOT_MEMBER","账号不是该群有效成员。");
        if(!membership.CanSendText || needsPhoto && !membership.CanSendPhotos)
            return Deny("NO_PERMISSION","账号在此群没有对应的消息发送权限。");
        return new(true,"OK","权限预检查通过；实际发送仍须遵守 Telegram 的实时慢速模式和限制。",
            membership.SlowModeDelaySeconds);
    }
}
