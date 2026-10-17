using System.Text.Json;

namespace Mosen.GroupScheduler.Telegram;

/// <summary>Allowlisted, operator-initiated single account operations. No unattended queue.</summary>
public static class TdlibManualOperations
{
    public static string Chats(string correlation,int limit=50)
    {
        if(limit is <1 or >100)throw new ArgumentOutOfRangeException(nameof(limit));
        return JsonSerializer.Serialize(new Dictionary<string,object>{
            ["@type"]="getChats",["@extra"]=correlation,
            ["chat_list"]=new Dictionary<string,object>{{"@type","chatListMain"}},
            ["limit"]=limit
        });
    }
    public static string Chat(string correlation,long id)
    {
        if(id==0)throw new ArgumentOutOfRangeException(nameof(id));
        return JsonSerializer.Serialize(new Dictionary<string,object>{
            ["@type"]="getChat",["@extra"]=correlation,["chat_id"]=id
        });
    }
    public static string SendOneText(string correlation,long id,string content)
    {
        if(id==0)throw new ArgumentOutOfRangeException(nameof(id));
        if(string.IsNullOrWhiteSpace(content)||content.Length>2000)
            throw new ArgumentException("Only single, bounded manually initiated text is supported.",nameof(content));
        if(content.Any(x=>char.IsControl(x)&&x is not '\n' and not '\t' and not '\r'))
            throw new ArgumentException("Invalid message characters.",nameof(content));
        return JsonSerializer.Serialize(new Dictionary<string,object?>{
            ["@type"]="sendMessage",["@extra"]=correlation,["chat_id"]=id,
            ["message_thread_id"]=0L,["reply_to"]=null,
            ["options"]=new Dictionary<string,object?>{
                ["disable_notification"]=false,["from_background"]=false,
                ["protect_content"]=false
            },
            ["input_message_content"]=new Dictionary<string,object?>{
                ["@type"]="inputMessageText",
                ["text"]=new Dictionary<string,object?>{
                    ["@type"]="formattedText",["text"]=content,
                    ["entities"]=Array.Empty<object>()
                },
                ["link_preview_options"]=new Dictionary<string,object?>{{"is_disabled",true}},
                ["clear_draft"]=false
            }
        });
    }
}
