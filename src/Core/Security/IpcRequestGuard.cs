using System.Text.Json;

namespace Mosen.GroupScheduler.Core.Security;

public enum AllowedIpcOperation { SystemPing, AppInfo, SettingsGet, SettingsSave, DiagnosticsGet, AccountsList, AccountsCreate, AccountsEdit, AccountsArchive, AccountsRestore, GroupsList, GroupsTopics, GroupsMembers }

/// <summary>Fail-closed JSON input gate: exact origin is checked separately by WebView2 host.</summary>
public static class IpcRequestGuard
{
    private const int MaxMessageChars = 8192;
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
                case "app.info": operation=AllowedIpcOperation.AppInfo; break;
                case "settings.get": operation=AllowedIpcOperation.SettingsGet; break;
                case "settings.save": operation=AllowedIpcOperation.SettingsSave; break;
                case "diagnostics.get": operation=AllowedIpcOperation.DiagnosticsGet; break;
                case "accounts.list": operation=AllowedIpcOperation.AccountsList; break;
                case "accounts.create": operation=AllowedIpcOperation.AccountsCreate; break;
                case "accounts.edit": operation=AllowedIpcOperation.AccountsEdit; break;
                case "accounts.archive": operation=AllowedIpcOperation.AccountsArchive; break;
                case "accounts.restore": operation=AllowedIpcOperation.AccountsRestore; break;
                case "groups.list": operation=AllowedIpcOperation.GroupsList; break;
                case "groups.topics": operation=AllowedIpcOperation.GroupsTopics; break;
                case "groups.members": operation=AllowedIpcOperation.GroupsMembers; break;
                default: return false;
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
