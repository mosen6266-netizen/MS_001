using System.Text.Json;

namespace Mosen.GroupScheduler.Core.Security;

public enum AllowedIpcOperation { SystemPing, AppInfo, SettingsGet, SettingsSave, DiagnosticsGet, AccountsList, AccountsCreate, AccountsEdit, AccountsArchive, AccountsRestore, GroupsList, GroupsTopics, GroupsMembers, ScriptsList, ScriptsGet, ScriptsCreate, ScriptsSave, ScriptsDuplicate, ScriptsArchive, ScriptsRestore, ScriptsExport, ScriptsImport, ScriptsPublish, ScriptsVersions, ScriptsPreview, ScriptsPreflight, MediaImport, MediaPreview, RuntimeSnapshot, ApiConfigStatus, ApiConfigSave, ApiConfigClear, TelegramAuthStatus, TelegramAuthStart, TelegramAuthSubmit, TelegramAuthStop, TelegramChatsRefresh, TelegramManualMessage }

/// <summary>Fail-closed JSON input gate: exact origin is checked separately by WebView2 host.</summary>
public static class IpcRequestGuard
{
    private const int MaxMessageChars = 7400000;
    private const int DraftMessageLimit = 524288;
    private const int StandardMessageLimit = 8192;
    private static readonly HashSet<string> RootReadFields = ["protocol","requestId","type"];
    private static readonly HashSet<string> RootWriteFields = ["protocol","requestId","type","payload"];
    private static readonly HashSet<string> PayloadFields = ["expectedRevision","preferences"];
    private static readonly HashSet<string> SettingsFields =
        ["density","searchDebounceMs","pageSize","desktopNotifications"];
    public static bool TryValidate(string? raw, out AllowedIpcOperation operation)
    {
        operation = default;
        if (raw is null || raw.Length is < 25 or > MaxMessageChars) return false;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(raw, new JsonDocumentOptions { MaxDepth=6, AllowTrailingCommas=false, CommentHandling=JsonCommentHandling.Disallow });
            JsonElement root = doc.RootElement;
            if (!ExactFields(root, RootReadFields) && !ExactFields(root, RootWriteFields)) return false;
            if (!root.TryGetProperty("protocol", out JsonElement protocol) || protocol.ValueKind != JsonValueKind.String ||
                protocol.GetString() != "mosen.desktop.v1") return false;
            if (!root.TryGetProperty("requestId", out JsonElement id) || id.ValueKind != JsonValueKind.String) return false;
            string? identifier = id.GetString();
            if (identifier is null || identifier.Length is < 1 or > 64 ||
                identifier.Any(c => !(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-'))) return false;
            if (!root.TryGetProperty("type", out JsonElement type) || type.ValueKind != JsonValueKind.String) return false;
            switch (type.GetString())
            {
                case "system.ping": operation=AllowedIpcOperation.SystemPing; break;
                case "telegram.api.status": operation=AllowedIpcOperation.ApiConfigStatus; break;
                case "telegram.api.save": operation=AllowedIpcOperation.ApiConfigSave; break;
                case "telegram.api.clear": operation=AllowedIpcOperation.ApiConfigClear; break;
                case "telegram.auth.status": operation=AllowedIpcOperation.TelegramAuthStatus; break;
                case "telegram.auth.start": operation=AllowedIpcOperation.TelegramAuthStart; break;
                case "telegram.auth.submit": operation=AllowedIpcOperation.TelegramAuthSubmit; break;
                case "telegram.auth.stop": operation=AllowedIpcOperation.TelegramAuthStop; break;
                case "telegram.chats.refresh": operation=AllowedIpcOperation.TelegramChatsRefresh; break;
                case "telegram.message.send": operation=AllowedIpcOperation.TelegramManualMessage; break;
                case "app.info": operation=AllowedIpcOperation.AppInfo; break;
                case "settings.get": operation=AllowedIpcOperation.SettingsGet; break;
                case "settings.save": operation=AllowedIpcOperation.SettingsSave; break;
                case "diagnostics.get": operation=AllowedIpcOperation.DiagnosticsGet; break;
                case "runtime.snapshot": operation=AllowedIpcOperation.RuntimeSnapshot; break;
                case "accounts.list": operation=AllowedIpcOperation.AccountsList; break;
                case "accounts.create": operation=AllowedIpcOperation.AccountsCreate; break;
                case "accounts.edit": operation=AllowedIpcOperation.AccountsEdit; break;
                case "accounts.archive": operation=AllowedIpcOperation.AccountsArchive; break;
                case "accounts.restore": operation=AllowedIpcOperation.AccountsRestore; break;
                case "groups.list": operation=AllowedIpcOperation.GroupsList; break;
                case "groups.topics": operation=AllowedIpcOperation.GroupsTopics; break;
                case "groups.members": operation=AllowedIpcOperation.GroupsMembers; break;
                case "scripts.list": operation=AllowedIpcOperation.ScriptsList; break;
                case "scripts.get": operation=AllowedIpcOperation.ScriptsGet; break;
                case "scripts.create": operation=AllowedIpcOperation.ScriptsCreate; break;
                case "scripts.save": operation=AllowedIpcOperation.ScriptsSave; break;
                case "scripts.duplicate": operation=AllowedIpcOperation.ScriptsDuplicate; break;
                case "scripts.archive": operation=AllowedIpcOperation.ScriptsArchive; break;
                case "scripts.restore": operation=AllowedIpcOperation.ScriptsRestore; break;
                case "scripts.export": operation=AllowedIpcOperation.ScriptsExport; break;
                case "scripts.import": operation=AllowedIpcOperation.ScriptsImport; break;
                case "scripts.publish": operation=AllowedIpcOperation.ScriptsPublish; break;
                case "scripts.versions": operation=AllowedIpcOperation.ScriptsVersions; break;
                case "scripts.preview": operation=AllowedIpcOperation.ScriptsPreview; break;
                case "scripts.preflight": operation=AllowedIpcOperation.ScriptsPreflight; break;
                case "media.import": operation=AllowedIpcOperation.MediaImport; break;
                case "media.preview": operation=AllowedIpcOperation.MediaPreview; break;
                default: return false;
            }
            if(operation==AllowedIpcOperation.TelegramAuthSubmit)
            {
                if(!ExactFields(root,RootWriteFields)||!root.TryGetProperty("payload",out var data)||
                   !ExactFields(data,new HashSet<string>{"action","input"}))return false;
                var action=data.GetProperty("action");
                var input=data.GetProperty("input");
                if(action.ValueKind!=JsonValueKind.String||input.ValueKind!=JsonValueKind.String)return false;
                string? task=action.GetString(),value=input.GetString();
                if(value is null||value.Length>512)return false;
                return task switch {
                    "qr"=>value.Length==0,
                    "phone"=>value.Length>=7&&value.Length<=20&&value[0]=='+'&&value.Skip(1).All(char.IsAsciiDigit),
                    "code" or "emailCode"=>value.Length is >=2 and <=32&&value.All(char.IsLetterOrDigit),
                    "email"=>value.Length is >=3 and <=254&&value.Contains('@')&&!value.Any(char.IsWhiteSpace),
                    "password"=>value.Length is >=1 and <=512,
                    _=>false
                };
            }
            if(operation==AllowedIpcOperation.TelegramManualMessage)
            {
                if(!ExactFields(root,RootWriteFields)||!root.TryGetProperty("payload",out var msg)||
                   !ExactFields(msg,new HashSet<string>{"chatId","text"}))return false;
                if(msg.GetProperty("chatId").ValueKind!=JsonValueKind.String||
                   msg.GetProperty("text").ValueKind!=JsonValueKind.String)return false;
                var targetChatId=msg.GetProperty("chatId").GetString();
                var body=msg.GetProperty("text").GetString();
                return targetChatId is {Length:>=2 and <=20}&&targetChatId.StartsWith("-",StringComparison.Ordinal)&&
                    long.TryParse(targetChatId,out long groupId)&&groupId<0 &&
                    body is {Length:>=1 and <=2000}&& !string.IsNullOrWhiteSpace(body)&&
                    !body.Any(x=>char.IsControl(x)&&x is not '\n' and not '\r' and not '\t');
            }
            if(operation is AllowedIpcOperation.TelegramAuthStatus or AllowedIpcOperation.TelegramAuthStart
                or AllowedIpcOperation.TelegramAuthStop or AllowedIpcOperation.TelegramChatsRefresh)
                return ExactFields(root,RootReadFields);
            if(operation==AllowedIpcOperation.ApiConfigSave)
            {
                if(!ExactFields(root,RootWriteFields) || !root.TryGetProperty("payload",out JsonElement config) ||
                   !ExactFields(config,new HashSet<string>{"apiId","apiHash"}))return false;
                var idValue=config.GetProperty("apiId");
                var hashValue=config.GetProperty("apiHash");
                return idValue.ValueKind==JsonValueKind.Number && idValue.TryGetInt32(out int apiId) &&
                    apiId>0 && hashValue.ValueKind==JsonValueKind.String &&
                    hashValue.GetString() is string hash && hash.Length==32 &&
                    hash.All(Uri.IsHexDigit) && raw.Length<=512;
            }
            if(operation is AllowedIpcOperation.ApiConfigStatus or AllowedIpcOperation.ApiConfigClear)
                return ExactFields(root,RootReadFields);
            if(operation==AllowedIpcOperation.ScriptsSave && raw.Length>DraftMessageLimit)return false;
            if(operation is not (AllowedIpcOperation.ScriptsSave or AllowedIpcOperation.MediaImport)
                && raw.Length>StandardMessageLimit)return false;
            if(operation is AllowedIpcOperation.MediaImport or AllowedIpcOperation.MediaPreview)
            {
                if(!ExactFields(root,RootWriteFields) || !root.TryGetProperty("payload",out var body))
                    return false;
                return ValidateMediaPayload(operation,body);
            }
            if(operation is AllowedIpcOperation.ScriptsList or AllowedIpcOperation.ScriptsGet or
                AllowedIpcOperation.ScriptsCreate or AllowedIpcOperation.ScriptsSave or
                AllowedIpcOperation.ScriptsDuplicate or AllowedIpcOperation.ScriptsArchive or
                AllowedIpcOperation.ScriptsRestore or AllowedIpcOperation.ScriptsExport or
                AllowedIpcOperation.ScriptsImport or AllowedIpcOperation.ScriptsPublish or
                AllowedIpcOperation.ScriptsVersions or AllowedIpcOperation.ScriptsPreview or
                AllowedIpcOperation.ScriptsPreflight)
            {
                if(!ExactFields(root,RootWriteFields) || !root.TryGetProperty("payload",out var data))
                    return false;
                return ValidateScriptsPayload(operation,data);
            }
            if (operation == AllowedIpcOperation.SettingsSave)
            {
                if (!ExactFields(root, RootWriteFields) ||
                    !root.TryGetProperty("payload", out JsonElement payload) ||
                    !ExactFields(payload, PayloadFields)) return false;
                if (!payload.TryGetProperty("expectedRevision", out JsonElement revision) ||
                    revision.ValueKind != JsonValueKind.Number || !revision.TryGetInt64(out long rev) || rev < 0) return false;
                if (!payload.TryGetProperty("preferences", out JsonElement prefs) || !ExactFields(prefs, SettingsFields)) return false;
                return EnumValue(prefs, "density", "comfortable", "compact")
                    && IntegerValue(prefs,"searchDebounceMs",150,250)
                    && (IntegerValue(prefs,"pageSize",10,10) ||
                        IntegerValue(prefs,"pageSize",20,20) ||
                        IntegerValue(prefs,"pageSize",50,50))
                    && prefs.GetProperty("desktopNotifications").ValueKind is JsonValueKind.True or JsonValueKind.False;
            }
            if (operation is AllowedIpcOperation.AccountsList or AllowedIpcOperation.AccountsCreate
                or AllowedIpcOperation.AccountsEdit or AllowedIpcOperation.AccountsArchive
                or AllowedIpcOperation.AccountsRestore)
            {
                if (!ExactFields(root,RootWriteFields) || !root.TryGetProperty("payload",out var payload))
                    return false;
                return ValidateAccountPayload(operation,payload);
            }
            if (operation is AllowedIpcOperation.GroupsList or AllowedIpcOperation.GroupsTopics or AllowedIpcOperation.GroupsMembers)
            {
                if (!ExactFields(root,RootWriteFields) || !root.TryGetProperty("payload",out var payload))
                    return false;
                return ValidateGroupPayload(operation,payload);
            }
            return ExactFields(root, RootReadFields); // remaining read operations have no payload
        }
        catch (JsonException) { return false; }
    }

    private static bool ValidateMediaPayload(AllowedIpcOperation op,JsonElement p)
    {
        if(op==AllowedIpcOperation.MediaImport)
        {
            if(!ExactFields(p,new HashSet<string>{"fileName","mimeType","base64"}))return false;
            if(!Label(p,"fileName",128,false))return false;
            if(p.GetProperty("mimeType").ValueKind!=JsonValueKind.String ||
               p.GetProperty("mimeType").GetString() is not ("image/png" or "image/jpeg" or "image/webp"))
                return false;
            var payload=p.GetProperty("base64");
            if(payload.ValueKind!=JsonValueKind.String)return false;
            string? base64=payload.GetString();
            return base64 is {Length:>=40 and <=6990516} &&
                base64.All(c=>c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '+' or '/' or '=');
        }
        if(!ExactFields(p,new HashSet<string>{"reference"}))return false;
        JsonElement reference=p.GetProperty("reference");
        if(!ExactFields(reference,new HashSet<string>{"assetId","mimeType","byteLength","width","height","fileName"}))
            return false;
        if(reference.GetProperty("assetId").ValueKind!=JsonValueKind.String ||
           reference.GetProperty("assetId").GetString() is not string hash || hash.Length!=64 ||
           hash.Any(c=>c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            return false;
        if(reference.GetProperty("mimeType").ValueKind!=JsonValueKind.String ||
           reference.GetProperty("mimeType").GetString() is not ("image/png" or "image/jpeg" or "image/webp"))
            return false;
        return IntegerValue(reference,"width",1,8192)&&IntegerValue(reference,"height",1,8192)&&
            Label(reference,"fileName",128,false) &&
            !reference.GetProperty("fileName").GetString()!.Any(c=>c is '/' or '\\' or ':') &&
            reference.GetProperty("byteLength").ValueKind==JsonValueKind.Number &&
            reference.GetProperty("byteLength").TryGetInt64(out long count) && count is >=32 and <=5242880;
    }

    private static bool ValidateScriptsPayload(AllowedIpcOperation operation,JsonElement p)
    {
        IReadOnlySet<string> fields=operation switch
        {
            AllowedIpcOperation.ScriptsList=>new HashSet<string>{"archived","query"},
            AllowedIpcOperation.ScriptsCreate=>new HashSet<string>{"title"},
            AllowedIpcOperation.ScriptsSave=>new HashSet<string>{"id","expectedRevision","document"},
            AllowedIpcOperation.ScriptsArchive=>new HashSet<string>{"id","expectedRevision","confirmation"},
            AllowedIpcOperation.ScriptsImport=>new HashSet<string>(),
            AllowedIpcOperation.ScriptsPublish=>new HashSet<string>{"id","expectedRevision"},
            AllowedIpcOperation.ScriptsPreview or AllowedIpcOperation.ScriptsPreflight=>
                operation==AllowedIpcOperation.ScriptsPreview?new HashSet<string>{"id","version"}:
                new HashSet<string>{"id","version","targets","choices"},
            AllowedIpcOperation.ScriptsRestore=>new HashSet<string>{"id","expectedRevision"},
            _=>new HashSet<string>{"id"}
        };
        if(!ExactFields(p,fields))return false;
        if(operation==AllowedIpcOperation.ScriptsImport)return true;
        if(operation==AllowedIpcOperation.ScriptsList)
            return p.GetProperty("archived").ValueKind is JsonValueKind.True or JsonValueKind.False
                && Label(p,"query",128,true);
        if(operation==AllowedIpcOperation.ScriptsCreate)
            return Label(p,"title",128,false);
        if(p.GetProperty("id").ValueKind!=JsonValueKind.String ||
           !Guid.TryParseExact(p.GetProperty("id").GetString(),"D",out var id) || id==Guid.Empty)return false;
        if(operation is AllowedIpcOperation.ScriptsSave or AllowedIpcOperation.ScriptsArchive or
           AllowedIpcOperation.ScriptsRestore or AllowedIpcOperation.ScriptsPublish)
        {
            if(p.GetProperty("expectedRevision").ValueKind!=JsonValueKind.Number ||
               !p.GetProperty("expectedRevision").TryGetInt64(out long revision) || revision<1)return false;
        }
        if(operation is AllowedIpcOperation.ScriptsPreview or AllowedIpcOperation.ScriptsPreflight)
        {
            if(!IntegerValue(p,"version",1,1000000))return false;
            if(operation==AllowedIpcOperation.ScriptsPreflight)return ValidatePreflightPayload(p);
        }
        if(operation==AllowedIpcOperation.ScriptsArchive)
            return p.GetProperty("confirmation").ValueKind==JsonValueKind.String &&
                p.GetProperty("confirmation").GetString()=="归档剧本";
        if(operation==AllowedIpcOperation.ScriptsSave)
        {
            JsonElement doc=p.GetProperty("document");
            if(doc.ValueKind!=JsonValueKind.Object ||
               !ExactFields(doc,new HashSet<string>{"title","roles","steps"}))return false;
            if(!doc.TryGetProperty("roles",out var roles)||roles.ValueKind!=JsonValueKind.Array||
               roles.GetArrayLength()>300 ||
               !doc.TryGetProperty("steps",out var steps)||steps.ValueKind!=JsonValueKind.Array||
               steps.GetArrayLength()>1000)return false;
            return Label(doc,"title",128,false); // deeper per-item validation in EditorDocumentRules.Parse
        }
        return true;
    }


    private static bool ValidatePreflightPayload(JsonElement p)
    {
        if(!p.TryGetProperty("targets",out var groups)||groups.ValueKind!=JsonValueKind.Array||
           groups.GetArrayLength() is <1 or >50 ||
           !p.TryGetProperty("choices",out var choices)||choices.ValueKind!=JsonValueKind.Array||
           choices.GetArrayLength()>200)return false;
        static bool ValidChat(JsonElement item)
        {
            if(!item.TryGetProperty("chatId",out var chat)||chat.ValueKind!=JsonValueKind.String||
                !long.TryParse(chat.GetString(),out var parsed)||parsed==0)return false;
            if(item.TryGetProperty("topicId",out var topic)&&
               (topic.ValueKind!=JsonValueKind.String||!long.TryParse(topic.GetString(),out var t)||t<=0))
                return false;
            return true;
        }
        foreach(var target in groups.EnumerateArray())
        {
            if(!ExactFields(target,new HashSet<string>{"chatId"}) &&
               !ExactFields(target,new HashSet<string>{"chatId","topicId"}))return false;
            if(!ValidChat(target))return false;
        }
        foreach(var choice in choices.EnumerateArray())
        {
            if(!ExactFields(choice,new HashSet<string>{"chatId","roleKey","accountId"}) &&
               !ExactFields(choice,new HashSet<string>{"chatId","topicId","roleKey","accountId"}))return false;
            if(!ValidChat(choice))return false;
            if(choice.GetProperty("roleKey").ValueKind!=JsonValueKind.String||
               !Guid.TryParseExact(choice.GetProperty("roleKey").GetString(),"D",out var role)||role==Guid.Empty ||
               choice.GetProperty("accountId").ValueKind!=JsonValueKind.String||
               !Guid.TryParseExact(choice.GetProperty("accountId").GetString(),"D",out var account)||account==Guid.Empty)
                return false;
        }
        return true;
    }

    private static bool ValidateGroupPayload(AllowedIpcOperation operation,JsonElement payload)
    {
        if(operation==AllowedIpcOperation.GroupsList)
        {
            if(!ExactFields(payload,new HashSet<string>{"query","page","pageSize"}))return false;
            return Label(payload,"query",128,true)&&
                IntegerValue(payload,"page",1,1000000)&&
                (IntegerValue(payload,"pageSize",10,10)||
                 IntegerValue(payload,"pageSize",20,20)||
                 IntegerValue(payload,"pageSize",50,50));
        }
        if(operation==AllowedIpcOperation.GroupsTopics)
        {
            if(!ExactFields(payload,new HashSet<string>{"chatId","page","pageSize"}))return false;
            return SafeChatId(payload) && IntegerValue(payload,"page",1,1000000) &&
                (IntegerValue(payload,"pageSize",10,10) ||
                 IntegerValue(payload,"pageSize",20,20) ||
                 IntegerValue(payload,"pageSize",50,50));
        }
        return ExactFields(payload,new HashSet<string>{"chatId"}) && SafeChatId(payload);
    }
    private static bool SafeChatId(JsonElement payload)
    {
        if(payload.GetProperty("chatId").ValueKind!=JsonValueKind.String)return false;
        string? id=payload.GetProperty("chatId").GetString();
        return id is {Length:>0 and <=20} &&
            long.TryParse(id,System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,out var parsed) && parsed!=0;
    }

    private static bool ValidateAccountPayload(AllowedIpcOperation action,JsonElement body)
    {
        IReadOnlySet<string> fields=action switch {
            AllowedIpcOperation.AccountsList=>new HashSet<string>{ "query","group","page","pageSize","archived" },
            AllowedIpcOperation.AccountsCreate=>new HashSet<string>{ "alias","groupTag" },
            AllowedIpcOperation.AccountsEdit=>new HashSet<string>{ "id","expectedRevision","alias","groupTag" },
            AllowedIpcOperation.AccountsArchive=>new HashSet<string>{ "id","expectedRevision","typedAlias" },
            AllowedIpcOperation.AccountsRestore=>new HashSet<string>{ "id","expectedRevision" },
            _=>new HashSet<string>()
        };
        if(!ExactFields(body,fields))return false;
        bool validId = action is AllowedIpcOperation.AccountsList or AllowedIpcOperation.AccountsCreate
            || (body.GetProperty("id").ValueKind==JsonValueKind.String &&
                Guid.TryParseExact(body.GetProperty("id").GetString(),"D",out var id) && id!=Guid.Empty);
        if(!validId)return false;
        bool revision = action is not (AllowedIpcOperation.AccountsEdit or AllowedIpcOperation.AccountsArchive
             or AllowedIpcOperation.AccountsRestore) ||
            (body.GetProperty("expectedRevision").ValueKind==JsonValueKind.Number &&
             body.GetProperty("expectedRevision").TryGetInt64(out var rev) && rev>0);
        if(!revision)return false;
        return action switch {
            AllowedIpcOperation.AccountsList =>
                Label(body,"query",128,true)&&Label(body,"group",64,true)&&
                body.GetProperty("archived").ValueKind is (JsonValueKind.True or JsonValueKind.False) &&
                IntegerValue(body,"page",1,1000000)&&
                (IntegerValue(body,"pageSize",10,10)||IntegerValue(body,"pageSize",20,20)||IntegerValue(body,"pageSize",50,50)),
            AllowedIpcOperation.AccountsCreate or AllowedIpcOperation.AccountsEdit =>
                Label(body,"alias",64,false)&&Label(body,"groupTag",64,true),
            AllowedIpcOperation.AccountsArchive=>Label(body,"typedAlias",64,false),
            AllowedIpcOperation.AccountsRestore=>true,
            _=>false
        };
    }
    private static bool Label(JsonElement body,string key,int maximum,bool allowEmpty)
    {
        if(body.GetProperty(key).ValueKind!=JsonValueKind.String)return false;
        string? s=body.GetProperty(key).GetString();
        return s is not null && s.Length<=maximum && (allowEmpty || !string.IsNullOrWhiteSpace(s)) &&
            !s.Any(char.IsControl);
    }

    private static bool ExactFields(JsonElement obj, IReadOnlySet<string> expected)
    {
        if (obj.ValueKind != JsonValueKind.Object) return false;
        string[] fields = obj.EnumerateObject().Select(x=>x.Name).ToArray();
        return fields.Length == expected.Count && fields.Distinct(StringComparer.Ordinal).Count() == fields.Length
            && fields.All(expected.Contains);
    }
    private static bool EnumValue(JsonElement obj,string key,string a,string b) =>
        obj.GetProperty(key) is var v && v.ValueKind == JsonValueKind.String && (v.GetString() == a || v.GetString() == b);
    private static bool IntegerValue(JsonElement obj,string key,int lower,int upper) =>
        obj.GetProperty(key) is var v && v.ValueKind == JsonValueKind.Number &&
        v.TryGetInt32(out int value) && value >= lower && value <= upper;
}
