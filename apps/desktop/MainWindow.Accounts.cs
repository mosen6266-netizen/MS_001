using System;
using System.Text.Json;
using System.Threading.Tasks;
using Mosen.GroupScheduler.Persistence;

namespace Mosen.GroupScheduler.Desktop;

public partial class MainWindow
{
    private readonly AccountRegistry _accountRegistry=new(AppDatabase.ForCurrentUser());

    private async Task HandleAccountRequestAsync(string raw,string requestId,string type)
    {
        try
        {
            using var json=JsonDocument.Parse(raw);
            JsonElement p=json.RootElement.GetProperty("payload");
            object output=await Task.Run<object>(()=>type switch
            {
                "accounts.list" => _accountRegistry.Find(
                    p.GetProperty("query").GetString()!,
                    p.GetProperty("group").GetString() is string g && g.Length>0 ? g : null,
                    p.GetProperty("page").GetInt32(),p.GetProperty("pageSize").GetInt32(),
                    p.GetProperty("archived").GetBoolean()),
                "accounts.create" => _accountRegistry.Register(p.GetProperty("alias").GetString()!,
                    p.GetProperty("groupTag").GetString()!),
                "accounts.edit" => _accountRegistry.Edit(
                    Guid.Parse(p.GetProperty("id").GetString()!),
                    p.GetProperty("expectedRevision").GetInt64(),
                    p.GetProperty("alias").GetString()!,p.GetProperty("groupTag").GetString()!),
                "accounts.archive" => ArchiveWithConfirmation(p),
                "accounts.restore" => RestoreArchived(p),
                _=>throw new ArgumentOutOfRangeException(nameof(type))
            });
            SendSettingResponse(requestId,type+".result",true,output);
        }
        catch(AccountVersionConflict)
        {
            SendSettingResponse(requestId,"accounts.error",false,new {code="CONFLICT",message="账号数据已被修改，请刷新列表重试。"});
        }
        catch(InvalidOperationException)
        {
            SendSettingResponse(requestId,"accounts.error",false,new {code="BLOCKED",message="操作被安全规则阻止：请检查账号限制、删除确认或群组任务绑定。"});
        }
        catch(Exception e) when(e is ArgumentException or FormatException or JsonException or OverflowException)
        {
            SendSettingResponse(requestId,"accounts.error",false,new {code="INVALID",message="账号参数不符合要求。"});
        }
        catch(Exception)
        {
            SendSettingResponse(requestId,"accounts.error",false,new {code="UNAVAILABLE",message="无法读取本地账号资料，请检查本地数据库。"});
        }
    }
    private object ArchiveWithConfirmation(JsonElement payload)
    {
        _accountRegistry.Archive(Guid.Parse(payload.GetProperty("id").GetString()!),
            payload.GetProperty("expectedRevision").GetInt64(),payload.GetProperty("typedAlias").GetString()!);
        return new {archived=true}; // protected TDLib files are never deleted by this action
    }
    private object RestoreArchived(JsonElement payload)
    {
        _accountRegistry.Restore(Guid.Parse(payload.GetProperty("id").GetString()!),
            payload.GetProperty("expectedRevision").GetInt64());
        return new {restored=true};
    }
}
