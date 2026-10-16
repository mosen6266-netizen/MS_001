using System.Text.Json;

namespace Mosen.GroupScheduler.Core.Security;

public enum AllowedIpcOperation { SystemPing, AppInfo, SettingsGet, SettingsSave, DiagnosticsGet }

/// <summary>Fail-closed JSON input gate: exact origin is checked separately by WebView2 host.</summary>
public static class IpcRequestGuard
{
    private const int MaxMessageChars = 8192;
    private static readonly HashSet<string> RootReadFields = ["protocol","requestId","type"];
    private static readonly HashSet<string> RootWriteFields = ["protocol","requestId","type","payload"];
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
                default: return false;
            }
            if (operation == AllowedIpcOperation.SettingsSave)
            {
                if (!ExactFields(root, RootWriteFields) ||
                    !root.TryGetProperty("payload", out JsonElement payload) ||
                    !ExactFields(payload, ["expectedRevision","preferences"])) return false;
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
            return ExactFields(root, RootReadFields); // all read operations have *no* payload
        }
        catch (JsonException) { return false; }
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
