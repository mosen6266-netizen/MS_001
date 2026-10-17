using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Mosen.GroupScheduler.Core.Security;
using Mosen.GroupScheduler.Installation;

namespace Mosen.GroupScheduler.Desktop;

public partial class MainWindow
{
    private readonly TelegramAppCredentialStore _telegramApiConfig =
        new(Path.Combine(UserDataPaths.ForCurrentUser(),"telegram-api-config.json"));

    private async Task HandleTelegramApiConfigurationAsync(string raw,string requestId,string type)
    {
        try
        {
            if(type=="telegram.api.status")
            {
                var present=await Task.Run(()=>_telegramApiConfig.Configured);
                SendSettingResponse(requestId,type+".result",true,new{
                    configured=present,storage="Windows DPAPI CurrentUser",
                    authorizedAccount=false,canSendMessages=false
                });
                return;
            }
            if(type=="telegram.api.clear")
            {
                await Task.Run(_telegramApiConfig.Clear);
                SendSettingResponse(requestId,type+".result",true,new{configured=false});
                return;
            }
            using var json=JsonDocument.Parse(raw);
            var payload=json.RootElement.GetProperty("payload");
            var appId=payload.GetProperty("apiId").GetInt32();
            var hash=payload.GetProperty("apiHash").GetString()!;
            await Task.Run(()=>_telegramApiConfig.Save(appId,hash));
            SendSettingResponse(requestId,type+".result",true,new{configured=true});
        }
        catch(ArgumentException)
        {
            SendSettingResponse(requestId,"telegram.api.error",false,
                new{code="INVALID_API_CONFIG",message="请输入有效的 Telegram API ID 和32位十六进制 API Hash。"});
        }
        catch(Exception)
        {
            SendSettingResponse(requestId,"telegram.api.error",false,
                new{code="API_CONFIG_UNAVAILABLE",message="配置未保存，请检查当前Windows用户和本地文件权限。"});
        }
    }
}
