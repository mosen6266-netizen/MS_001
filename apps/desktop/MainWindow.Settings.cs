using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Mosen.GroupScheduler.Persistence;

namespace Mosen.GroupScheduler.Desktop;

public partial class MainWindow
{
    private static readonly JsonSerializerOptions PreferenceJson = new(JsonSerializerDefaults.Web);
    private readonly PreferencesRepository _preferencesRepository = new(AppDatabase.ForCurrentUser());

    private async Task HandleSettingsRequestAsync(string sourceJson, string requestId, string type)
    {
        try
        {
            using var rootDoc = JsonDocument.Parse(sourceJson);
            var root = rootDoc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;
            var keys = root.EnumerateObject().Select(item => item.Name).ToArray();
            if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Length) return;
            if (type == "settings.get")
            {
                if (keys.Length != 3 || root.TryGetProperty("payload", out _)) return;
                var data = await Task.Run(_preferencesRepository.Read);
                SendSettingResponse(requestId, "settings.get.result", true, data);
                return;
            }
            if (keys.Length != 4 || !root.TryGetProperty("payload", out JsonElement payload)
                || payload.ValueKind != JsonValueKind.Object
                || payload.EnumerateObject().Count() != 2
                || !payload.TryGetProperty("expectedRevision", out JsonElement expected)
                || !expected.TryGetInt64(out long revision) || revision < 0
                || !payload.TryGetProperty("preferences", out JsonElement pref)
                || pref.ValueKind != JsonValueKind.Object
                || pref.EnumerateObject().Count() != 4) return;
            UserPreferences? values = pref.Deserialize<UserPreferences>(PreferenceJson);
            if (values is null) return;
            values.Validate();
            var saved = await Task.Run(() => _preferencesRepository.Save(revision, values));
            SendSettingResponse(requestId, "settings.save.result", true, saved);
        }
        catch (PreferencesConflictException)
        {
            SendSettingResponse(requestId, "settings.error", false, new { code = "CONFLICT", message = "设置已被其它操作修改，请重新读取。" });
        }
        catch (Exception e) when (e is JsonException or ArgumentException or OverflowException)
        {
            SendSettingResponse(requestId, "settings.error", false, new { code = "INVALID", message = "设置内容不符合允许范围。" });
        }
        catch (Exception)
        {
            // Do not log paths, token strings, or raw user data.
            SendSettingResponse(requestId, "settings.error", false, new { code = "UNAVAILABLE", message = "本地设置暂时无法保存，请检查磁盘权限或空间。" });
        }
    }

    private void SendSettingResponse(string requestId, string type, bool ok, object payload)
    {
        if (_closing || Browser.CoreWebView2 is null) return;
        Browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new {
            protocol = Protocol, requestId, type, ok, payload
        }, PreferenceJson));
    }
}
