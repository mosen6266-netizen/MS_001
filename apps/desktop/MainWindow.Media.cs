using System;
using System.Text.Json;
using System.Threading.Tasks;
using Mosen.GroupScheduler.Scripts.Editor;
using Mosen.GroupScheduler.Scripts.Media;

namespace Mosen.GroupScheduler.Desktop;
public partial class MainWindow
{
    private readonly MediaAssetStore _media=new(MediaAssetStore.ForCurrentUser().RootPath);

    private async Task HandleMediaRequestAsync(string raw,string id,string type)
    {
        try
        {
            using var root=JsonDocument.Parse(raw);
            var p=root.RootElement.GetProperty("payload");
            object output=await Task.Run<object>(()=>type switch
            {
                "media.import" => _media.ImportBase64(p.GetProperty("fileName").GetString()!,
                    p.GetProperty("mimeType").GetString()!,p.GetProperty("base64").GetString()!),
                "media.preview" => new {dataUrl=_media.PreviewDataUrl(
                    p.GetProperty("reference").Deserialize<MediaReference>(PreferenceJson) ??
                    throw new ArgumentException("Missing image metadata."))},
                _=>throw new ArgumentOutOfRangeException(nameof(type))
            });
            SendSettingResponse(id,type+".result",true,output);
        }
        catch(Exception ex) when(ex is ArgumentException or JsonException or FormatException)
        {
            SendSettingResponse(id,"media.error",false,new{code="INVALID",
                message="图片格式或尺寸不受支持。请选择不超过5MB的PNG、JPEG或WebP图片。"});
        }
        catch(Exception)
        {
            SendSettingResponse(id,"media.error",false,new{code="UNAVAILABLE",
                message="本地图片无法保存或预览，请检查存储空间和图片是否损坏。"});
        }
    }
}
