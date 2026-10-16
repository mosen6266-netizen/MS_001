using System;
using System.Text.Json;
using System.Threading.Tasks;
using Mosen.GroupScheduler.Persistence;
using Mosen.GroupScheduler.Scripts.Editor;

namespace Mosen.GroupScheduler.Desktop;
public partial class MainWindow
{
    private readonly EditorDraftRepository _editor = new(AppDatabase.ForCurrentUser());

    private async Task HandleScriptRequestAsync(string raw,string id,string type)
    {
        try
        {
            using var root=JsonDocument.Parse(raw);
            var p=root.RootElement.GetProperty("payload");
            object output=await Task.Run<object>(()=>type switch
            {
                "scripts.list" => new {items=_editor.List(
                    p.GetProperty("archived").GetBoolean(),p.GetProperty("query").GetString()!)},
                "scripts.get" => _editor.Get(Guid.Parse(p.GetProperty("id").GetString()!),
                    includeArchived:true) ?? throw new InvalidOperationException("Draft not found"),
                "scripts.create" => _editor.Create(p.GetProperty("title").GetString()!),
                "scripts.save" => _editor.Save(Guid.Parse(p.GetProperty("id").GetString()!),
                    p.GetProperty("expectedRevision").GetInt64(),
                    EditorDocumentRules.Parse(p.GetProperty("document"))),
                "scripts.duplicate" => _editor.Duplicate(Guid.Parse(p.GetProperty("id").GetString()!)),
                "scripts.archive" => ArchiveScript(p),
                "scripts.restore" => RestoreScript(p),
                _=>throw new ArgumentOutOfRangeException(nameof(type))
            });
            SendSettingResponse(id,type+".result",true,output);
        }
        catch(DraftConflictException)
        {
            SendSettingResponse(id,"scripts.error",false,new{code="CONFLICT",
                message="剧本已在其他操作中变更，不能覆盖。请先保存当前编辑内容并重新读取。"});
        }
        catch(Exception e) when(e is ArgumentException or JsonException or FormatException)
        {
            SendSettingResponse(id,"scripts.error",false,new{code="INVALID",message="剧本格式、角色或条目不符合要求。"});
        }
        catch(InvalidOperationException)
        {
            SendSettingResponse(id,"scripts.error",false,new{code="BLOCKED",message="操作被安全规则阻止，剧本可能正在使用。"});
        }
        catch(Exception)
        {
            SendSettingResponse(id,"scripts.error",false,new{code="UNAVAILABLE",message="本地剧本操作无法完成，请检查磁盘与数据库。"});
        }
    }
    private object ArchiveScript(JsonElement p)
    {
        _editor.Archive(Guid.Parse(p.GetProperty("id").GetString()!),
            p.GetProperty("expectedRevision").GetInt64(),p.GetProperty("confirmation").GetString()!);
        return new{archived=true};
    }
    private object RestoreScript(JsonElement p)
    {
        _editor.Restore(Guid.Parse(p.GetProperty("id").GetString()!),p.GetProperty("expectedRevision").GetInt64());
        return new{restored=true};
    }
}
