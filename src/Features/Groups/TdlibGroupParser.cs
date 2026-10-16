using System.Text.Json;
using Mosen.GroupScheduler.Telegram;

namespace Mosen.GroupScheduler.Groups;

/// <summary>Read-only TDLib response decoder. Only the native event coordinator can call
/// ingestion with a separately verified (accountId, clientId) association.
/// No arbitrary UI field may claim Telegram-trusted provenance.</summary>
public static class TdlibGroupParser
{
    public static GroupInfo? ReadChat(TdlibEnvelope envelope,DateTimeOffset now)
    {
        if(envelope.Type is not ("chat" or "updateNewChat"))return null;
        JsonElement chat=envelope.Body;
        if(envelope.Type=="updateNewChat" && (!chat.TryGetProperty("chat",out chat) ||
           chat.ValueKind!=JsonValueKind.Object))return null;
        if(!TryInt64(chat,"id",out long chatId)||chatId==0||!TryText(chat,"title",256,out string title))
            return null;
        if(!chat.TryGetProperty("type",out var kind)||kind.ValueKind!=JsonValueKind.Object ||
            !kind.TryGetProperty("@type",out var type))return null;
        string? label=type.GetString();
        string chatKind=label switch {
            "chatTypeBasicGroup"=>"group",
            "chatTypeSupergroup" => GetBool(kind,"is_channel") ? "channel":"supergroup",
            _=>"other"
        };
        // TDLib sometimes adds forum fields on the supergroup; missing => unknown(false), never assume topic support.
        bool forum=GetBool(chat,"is_forum") || GetBool(kind,"is_forum");
        if(chatKind=="other")return null;
        return new(chatId.ToString(System.Globalization.CultureInfo.InvariantCulture),title,chatKind,forum,now);
    }
    public static TopicInfo? ReadTopic(TdlibEnvelope envelope,string verifiedChatId,DateTimeOffset now)
    {
        if(envelope.Type is not ("forumTopic" or "forumTopicInfo" or "updateForumTopicInfo"))return null;
        JsonElement item=envelope.Body;
        if(item.TryGetProperty("info",out var info)&&info.ValueKind==JsonValueKind.Object)item=info;
        if(item.TryGetProperty("chat_id",out _) &&
           (!TryInt64(item,"chat_id",out var declared) ||
           declared.ToString(System.Globalization.CultureInfo.InvariantCulture)!=verifiedChatId))return null;
        if(!TryInt64(item,"message_thread_id",out long id)||id<=0 ||
           !TryText(item,"name",256,out string title))return null;
        return new(verifiedChatId,id.ToString(System.Globalization.CultureInfo.InvariantCulture),title,
            GetBool(item,"is_closed"),GetBool(item,"is_hidden"),now);
    }
    public static MembershipInfo? ReadMembership(
        TdlibEnvelope envelope,Guid verifiedAccountId,string verifiedChatId,
        int slowModeDelaySeconds,DateTimeOffset now)
    {
        if(envelope.Type!="chatMember"||verifiedAccountId==Guid.Empty||
           slowModeDelaySeconds is < 0 or > 3600)return null;
        var body=envelope.Body;
        if(!body.TryGetProperty("status",out var status)||status.ValueKind!=JsonValueKind.Object ||
           !status.TryGetProperty("@type",out var type)||type.ValueKind!=JsonValueKind.String)return null;
        string state=type.GetString() switch {
            "chatMemberStatusCreator"=>"creator",
            "chatMemberStatusAdministrator"=>"administrator",
            "chatMemberStatusMember"=>"member",
            "chatMemberStatusRestricted"=>"restricted",
            "chatMemberStatusLeft"=>"left",
            "chatMemberStatusBanned"=>"banned",
            _=>"unknown"
        };
        // Text/photo grants for ordinary members depend also on chat default permissions.
        // Until verified chat permissions are combined, fail closed rather than guessing.
        bool allowedMember=state is "member" or "administrator" or "creator" ||
             state=="restricted" && GetBool(status,"is_member");
        bool text=allowedMember && state is ("administrator" or "creator");
        bool photo=text;
        if(state=="restricted" && status.TryGetProperty("permissions",out var permissions))
        {
            text=GetBool(permissions,"can_send_basic_messages");
            photo=text && GetBool(permissions,"can_send_photos");
        }
        return new(verifiedAccountId,verifiedChatId,state,text,photo,slowModeDelaySeconds,now);
    }
    public static MembershipInfo ApplyChatPermissions(
        MembershipInfo member,JsonElement chatPermissions)
    {
        if(chatPermissions.ValueKind!=JsonValueKind.Object)return member with {CanSendText=false,CanSendPhotos=false};
        // Admin/creator status doesn't override channel write or topic closure decisions.
        bool text=GetBool(chatPermissions,"can_send_basic_messages");
        bool photo=GetBool(chatPermissions,"can_send_photos");
        return member with {
            CanSendText=member.Status switch {
                "creator" or "administrator"=>true,
                "member"=>text,
                "restricted"=>member.CanSendText && text,
                _=>false
            },
            CanSendPhotos=member.Status switch {
                "creator" or "administrator"=>true,
                "member"=>text && photo,
                "restricted"=>member.CanSendPhotos && text && photo,
                _=>false
            }
        };
    }
    public static int ReadSlowModeSeconds(TdlibEnvelope envelope)
    {
        if(envelope.Type is not ("supergroupFullInfo" or "updateSupergroupFullInfo"))return 0;
        JsonElement info=envelope.Body;
        if(envelope.Type=="updateSupergroupFullInfo" &&
            (!info.TryGetProperty("supergroup_full_info",out info) || info.ValueKind!=JsonValueKind.Object))return 0;
        return info.TryGetProperty("slow_mode_delay",out var delay) &&
            delay.ValueKind==JsonValueKind.Number &&
            delay.TryGetInt32(out int value) && value is >=0 and <=3600 ? value : 0;
    }

    private static bool GetBool(JsonElement element,string property)=>
        element.ValueKind==JsonValueKind.Object &&
        element.TryGetProperty(property,out var item) && item.ValueKind==JsonValueKind.True;
    private static bool TryInt64(JsonElement element,string key,out long value)
    {
        value=0;
        return element.ValueKind==JsonValueKind.Object && element.TryGetProperty(key,out var v) &&
            v.ValueKind==JsonValueKind.Number && v.TryGetInt64(out value);
    }
    private static bool TryText(JsonElement e,string key,int max,out string value)
    {
        value="";
        if(e.ValueKind!=JsonValueKind.Object||!e.TryGetProperty(key,out var v)||v.ValueKind!=JsonValueKind.String)
            return false;
        string? text=v.GetString();
        if(string.IsNullOrWhiteSpace(text)||text.Length>max||text.Any(char.IsControl))return false;
        value=text;
        return true;
    }
}
