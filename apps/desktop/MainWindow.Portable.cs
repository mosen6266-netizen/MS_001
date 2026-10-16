using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using SaveFileDialog=Microsoft.Win32.SaveFileDialog;
using OpenFileDialog=Microsoft.Win32.OpenFileDialog;
using Mosen.GroupScheduler.Persistence;
using Mosen.GroupScheduler.Scripts.Media;
using Mosen.GroupScheduler.Scripts.Portable;

namespace Mosen.GroupScheduler.Desktop;
public partial class MainWindow
{
    /// <summary>Only the OS file picker chooses a path: no WebView2 request contains one.</summary>
    private async Task HandlePortableScriptRequestAsync(string id,string type,string? scriptId)
    {
        if(type=="scripts.export")
        {
            var dialog=new SaveFileDialog {
                Title="导出便携剧本",Filter="MOSEN Telegram 剧本 (*.mosentgscript)|*.mosentgscript",
                DefaultExt=".mosentgscript",AddExtension=true,OverwritePrompt=true,
                FileName="剧本.mosentgscript"
            };
            if(dialog.ShowDialog(this)!=true)
            {
                SendSettingResponse(id,"scripts.export.result",true,new{cancelled=true});return;
            }
            try
            {
                await Task.Run(()=>{
                    var draft=_editor.Get(Guid.Parse(scriptId!))??throw new InvalidOperationException("Draft unavailable.");
                    byte[] bytes=PortableScriptPackage.Export(draft.Document,MediaAssetStore.ForCurrentUser());
                    string path=dialog.FileName;
                    string temp=path+".partial-"+Guid.NewGuid().ToString("N");
                    try
                    {
                        using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                        {stream.Write(bytes);stream.Flush(flushToDisk:true);}
                        File.Move(temp,path,overwrite:true);
                    }
                    finally {if(File.Exists(temp))File.Delete(temp);}
                });
                SendSettingResponse(id,"scripts.export.result",true,new{cancelled=false,saved=true});
            }
            catch(Exception)
            {
                SendSettingResponse(id,"scripts.error",false,new{
                    code="EXPORT_FAILED",message="剧本导出失败，请检查图片文件完整性及目标磁盘空间。"
                });
            }
            return;
        }
        var open=new OpenFileDialog {
            Title="导入便携剧本",Filter="MOSEN Telegram 剧本 (*.mosentgscript)|*.mosentgscript",
            DefaultExt=".mosentgscript",CheckFileExists=true,Multiselect=false
        };
        if(open.ShowDialog(this)!=true)
        {
            SendSettingResponse(id,"scripts.import.result",true,new{cancelled=true});return;
        }
        try
        {
            var value=await Task.Run<object>(()=>{
                var file=new FileInfo(open.FileName);
                if(file.Length is <32 or >PortableScriptPackage.MaxArchiveBytes)
                    throw new InvalidDataException("Archive exceeds size limit.");
                var decoded=PortableScriptPackage.Decode(File.ReadAllBytes(open.FileName));
                var media=MediaAssetStore.ForCurrentUser();
                PortableScriptPackage.InstallMedia(decoded,media);
                var newDraft=_editor.CreateDocument(decoded.Document);
                var registry=new AccountRegistry(AppDatabase.ForCurrentUser());
                var accounts=registry.Find("",null,1,50).Items.ToList();
                for(int page=2;page<=6;page++)
                {
                    var next=registry.Find("",null,page,50);
                    accounts.AddRange(next.Items);
                    if(accounts.Count>=next.Total)break;
                }
                var matches=RoleRemarkResolver.Resolve(decoded.Document,
                    accounts.Select(x=>new LocalRemarkCandidate(x.Id,x.Alias)));
                return new{cancelled=false,draft=newDraft,roleMatches=matches};
            });
            SendSettingResponse(id,"scripts.import.result",true,value);
        }
        catch(Exception)
        {
            SendSettingResponse(id,"scripts.error",false,new{
                code="IMPORT_REJECTED",message="剧本导入失败：ZIP格式、角色、图片校验或本地存储不符合要求。原有草稿未覆盖。"
            });
        }
    }
}
