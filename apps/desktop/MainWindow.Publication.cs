using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Mosen.GroupScheduler.Groups;
using Mosen.GroupScheduler.Persistence;
using Mosen.GroupScheduler.Preflight;
using Mosen.GroupScheduler.Scripts.Media;
using Mosen.GroupScheduler.Scripts.Editor;
using Mosen.GroupScheduler.Scripts.Preview;

namespace Mosen.GroupScheduler.Desktop;
public partial class MainWindow
{
    private readonly PublishedScriptRepository _publishedScripts = new(
        AppDatabase.ForCurrentUser(),MediaAssetStore.ForCurrentUser());
    private readonly GroupCatalogRepository _publishedGroups = new(AppDatabase.ForCurrentUser());

    private async Task HandlePublicationRequestAsync(string requestId,string action,JsonElement payload)
    {
        try
        {
            var result=await Task.Run<object>(()=>action switch
            {
                "scripts.publish" => PublishScript(payload),
                "scripts.versions" => ListPublishedVersions(payload),
                "scripts.preview" => PreviewPublishedVersion(payload),
                "scripts.preflight" => PreflightPublishedVersion(payload),
                _ => throw new ArgumentException("Unknown publication action.")
            });
            SendSettingResponse(requestId,action+".result",true,result);
        }
        catch(DraftConflictException)
        { SendSettingResponse(requestId,"scripts.error",false,new{code="CONFLICT",message="草稿版本已经变化，请先重新保存后再发布。"}); }
        catch(Exception)
        { SendSettingResponse(requestId,"scripts.error",false,new{code="PREVIEW_FAILED",
             message="发布、预览或预检失败；请检查草稿版本、图片完整性和群权限。"}); }
    }
    private object PublishScript(JsonElement p)
    {
        var published=_publishedScripts.Publish(Guid.Parse(p.GetProperty("id").GetString()!),
            p.GetProperty("expectedRevision").GetInt64());
        return new{version=published.Version,sourceRevision=published.SourceRevision,sha256=published.Sha256};
    }
    private object ListPublishedVersions(JsonElement p)
    {
        var items=_publishedScripts.List(Guid.Parse(p.GetProperty("id").GetString()!))
            .Select(x=>new{version=x.Version,sourceRevision=x.SourceRevision,sha256=x.Sha256}).ToArray();
        return new{items};
    }
    private PublishedScript ReadPublished(JsonElement p)
    {
        return _publishedScripts.Get(Guid.Parse(p.GetProperty("id").GetString()!),
            p.GetProperty("version").GetInt32())??throw new InvalidOperationException("Published version unavailable.");
    }
    private object PreviewPublishedVersion(JsonElement p)=>ScriptPreviewBuilder.Build(ReadPublished(p));
    private object PreflightPublishedVersion(JsonElement p)
    {
        var published=ReadPublished(p);
        var now=DateTimeOffset.UtcNow;
        var selectedTargets=p.GetProperty("targets").EnumerateArray().Select(x=>(
            ChatId:x.GetProperty("chatId").GetString()!,
            TopicId:x.TryGetProperty("topicId",out var topic)?topic.GetString():null)).ToArray();
        var choices=p.GetProperty("choices").EnumerateArray().Select(x=>new RoleChoice(
            x.GetProperty("chatId").GetString()!,
            x.TryGetProperty("topicId",out var topic)?topic.GetString():null,
            x.GetProperty("roleKey").GetString()!,
            Guid.Parse(x.GetProperty("accountId").GetString()!))).ToArray();
        var targets=new List<PreflightTarget>();
        foreach(var (chatId,topicId) in selectedTargets)
        {
            var group=_publishedGroups.GetChat(chatId);
            var topic=topicId is null?null:_publishedGroups.GetTopic(chatId,topicId);
            var accounts=_publishedGroups.FindMembers(chatId).Select(x=>new PreflightAccount(
                Guid.Parse(x.AccountId),x.Alias,
                // Persisted flags are NOT a live TDLib authorization proof.
                // Integration must inject official fresh authorizations before enabling real execution.
                LiveAuthorized:false,
                new MembershipInfo(Guid.Parse(x.AccountId),chatId,x.Status,x.CanSendText,x.CanSendPhotos,
                    x.SlowModeDelaySeconds,x.CheckedUtc))).ToArray();
            bool conflict;
            var db=AppDatabase.ForCurrentUser();db.Initialize();
            using(var sql=new SqliteConnection(new SqliteConnectionStringBuilder{
                DataSource=db.FilePath,Mode=SqliteOpenMode.ReadWrite,
                ForeignKeys=true,Pooling=false}.ToString()))
            {
                sql.Open();using var cmd=sql.CreateCommand();
                cmd.CommandText=@"SELECT COUNT(*) FROM runs WHERE chat_id=$chat
                    AND COALESCE(topic_id,'')=$topic AND state IN ('ready','running','paused','review');";
                cmd.Parameters.AddWithValue("$chat",chatId);
                cmd.Parameters.AddWithValue("$topic",topicId??"");
                conflict=Convert.ToInt64(cmd.ExecuteScalar())>0;
            }
            targets.Add(new(chatId,topicId,group,topic,accounts,
                MembershipsFreshForStart:false,HasConflictingTask:conflict));
        }
        var store=MediaAssetStore.ForCurrentUser();
        return PreflightPlanner.Check(published,targets,choices,m=>{
            try { _=store.PreviewDataUrl(m);return true; }
            catch(Exception) {return false;}
        },now);
    }
}
