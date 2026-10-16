using System;
using System.Text.Json;
using System.Threading.Tasks;
using Mosen.GroupScheduler.Groups;
using Mosen.GroupScheduler.Persistence;

namespace Mosen.GroupScheduler.Desktop;

public partial class MainWindow
{
    // Read-only mirror: currently empty until official TDLib identity and chat discovery
    // are connected in the later native account integration stage.
    private readonly GroupCatalogRepository _groups = new(AppDatabase.ForCurrentUser());

    private async Task HandleGroupRequestAsync(string raw,string id,string type)
    {
        try
        {
            using var json=JsonDocument.Parse(raw);
            var body=json.RootElement.GetProperty("payload");
            object result=await Task.Run<object>(()=>type switch
            {
                "groups.list" => _groups.FindGroups(body.GetProperty("query").GetString()!,
                    body.GetProperty("page").GetInt32(),body.GetProperty("pageSize").GetInt32()),
                "groups.topics" => _groups.FindTopics(body.GetProperty("chatId").GetString()!,
                    body.GetProperty("page").GetInt32(),body.GetProperty("pageSize").GetInt32()),
                "groups.members" => new {items=_groups.FindMembers(body.GetProperty("chatId").GetString()!)},
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            });
            SendSettingResponse(id,type+".result",true,result);
        }
        catch(Exception)
        {
            // Never include raw TDLib data, SQL, account ID or exception messages in errors.
            SendSettingResponse(id,"groups.error",false,new{code="UNAVAILABLE",message="群组目录暂不可用，请检查本地数据库。"});
        }
    }
}
