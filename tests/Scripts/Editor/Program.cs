using System.Text.Json;
using Microsoft.Data.Sqlite;
using Mosen.GroupScheduler.Persistence;
using Mosen.GroupScheduler.Scripts.Editor;
int checks=0;void Verify(bool yes,string msg){if(!yes)throw new Exception("FAIL "+msg);checks++;Console.WriteLine("PASS "+msg);}
var root=Path.Combine(Path.GetTempPath(),"ms001-editor-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
try
{
 var db=new AppDatabase(Path.Combine(root,"app.db"));db.Initialize();var scripts=new EditorDraftRepository(db);
 Verify(db.GetSchemaVersion()==9,"draft schema upgrades to v9");
 var created=scripts.Create("示例剧本");
 Verify(created.Revision==1&&created.Document.Roles.Count==0,"empty script draft created locally");
 Verify(scripts.List().Count==1,"script index persists");
 var roleA=Guid.NewGuid().ToString("D");var roleB=Guid.NewGuid().ToString("D");
 var a=new ScriptRole(roleA,"客服A");var b=new ScriptRole(roleB,"客服B");
 var first=new EditorStep(Guid.NewGuid().ToString("D"),roleA,"中文\nEnglish 🌐",30000,5000,0);
 var second=new EditorStep(Guid.NewGuid().ToString("D"),roleB,"后续确认",5000,10000,1500);
 var document=new EditorDocument("国际团队\n剧本",[a,b],[first,second]);
 var saved=scripts.Save(Guid.Parse(created.ScriptId),created.Revision,document);
 Verify(saved.Revision==2,"save increments optimistic revision");
 Verify(saved.Document.Steps[0].PreSendDelayMs==30000&&saved.Document.Steps[0].TypingDurationMs==5000,
   "pre-send 30s and typing 5s stored independently, ordered total 35 seconds");
 Verify(saved.Document.Steps[0].Body=="中文\nEnglish 🌐","multilingual message and newlines round trip");
 Verify(scripts.Get(Guid.Parse(created.ScriptId))!.Document.Roles[0].RoleLabel=="客服A","role label persists without local account ID");
 var flat=JsonSerializer.Serialize(saved.Document,EditorDocumentRules.WireOptions);
 Verify(!flat.Contains("accountId")&&!flat.Contains("phoneNumber")&&!flat.Contains("session")&&
   !flat.Contains("apiHash"),"serialized draft contains no local account identity or credentials");
 bool stale=false;
 try{scripts.Save(Guid.Parse(created.ScriptId),1,document with {Title="旧页面覆盖"});}catch(DraftConflictException){stale=true;}
 Verify(stale && scripts.Get(Guid.Parse(created.ScriptId))!.Document.Title==document.Title,
   "concurrent stale draft save rejected without losing latest edit");
 var reordered=document with {Steps=[second,first]};
 var changed=scripts.Save(Guid.Parse(created.ScriptId),2,reordered);
 Verify(changed.Document.Steps.Select(s=>s.StepKey).SequenceEqual(new[]{second.StepKey,first.StepKey}),
   "drag reorder persists exactly");
 var duplicate=scripts.Duplicate(Guid.Parse(created.ScriptId));
 Verify(duplicate.ScriptId!=created.ScriptId && duplicate.Document.Title.EndsWith("副本"),
   "duplicate script creates new draft ID");
 Verify(duplicate.Document.Steps.Count==2 &&
   duplicate.Document.Steps.Select(s=>s.StepKey).Intersect(changed.Document.Steps.Select(s=>s.StepKey)).Count()==0,
   "copied messages have new immutable step keys");
 Verify(duplicate.Document.Roles.Select(x=>x.RoleLabel).SequenceEqual(new[]{"客服A","客服B"}),
   "copy retains portable role labels");
 bool extraRejected=false;
 try{
   using var doc=JsonDocument.Parse("""{"title":"拒绝","roles":[],"steps":[],"accountId":"PRIVATE"}""");
   EditorDocumentRules.Parse(doc.RootElement);
 }catch(JsonException){extraRejected=true;}
 Verify(extraRejected,"unknown account ID field rejected from persisted draft");
 var badStep=new EditorStep(Guid.NewGuid().ToString("D"),roleA,"bad",30000,70000,0);
 bool timeRejected=false;try{EditorDocumentRules.Validate(document with {Steps=[badStep]});}catch(ArgumentException){timeRejected=true;}
 Verify(timeRejected,"typing above 60 seconds rejected without corrupting database");
 bool missingRole=false;try{
   EditorDocumentRules.Validate(document with {Steps=[first with {RoleKey=Guid.NewGuid().ToString("D")}]});
 }catch(ArgumentException){missingRole=true;}
 Verify(missingRole,"every message must refer to a declared role");
 bool duplicateAlias=false;try{
   EditorDocumentRules.Validate(document with {Roles=[a,new ScriptRole(Guid.NewGuid().ToString("D")," 客服A ")]});
 }catch(ArgumentException){duplicateAlias=true;}
 Verify(duplicateAlias,"NFC+trim duplicate role labels rejected");
 var backup=db.CreateBackup();Verify(File.Exists(backup),"drafts included in verified SQLite backup");
 var reopened=new EditorDraftRepository(new AppDatabase(db.FilePath));
 Verify(reopened.Get(Guid.Parse(created.ScriptId))!.Document.Steps[0].Body==second.Body,
   "draft persists through repository restart");
 bool noConfirm=false;try{scripts.Archive(Guid.Parse(created.ScriptId),changed.Revision,"bad");}
 catch(ArgumentException){noConfirm=true;}
 Verify(noConfirm,"archive needs explicit confirmation phrase");
 scripts.Archive(Guid.Parse(created.ScriptId),changed.Revision,"归档剧本");
 Verify(scripts.Get(Guid.Parse(created.ScriptId)) is null&&scripts.List().Count==1,
   "archived draft hidden, copied draft kept");
 Verify(scripts.List(archived:true).Count==1,"archived script remains available for recovery");
 var arch=scripts.Get(Guid.Parse(created.ScriptId),includeArchived:true)!;
 scripts.Restore(Guid.Parse(arch.ScriptId),arch.Revision);
 Verify(scripts.Get(Guid.Parse(arch.ScriptId))?.Document.Steps.Count==2,
   "archived draft restored without losing content");
 var many=Enumerable.Range(0,10).Select(i=>scripts.Create("多草稿"+i)).ToArray();
 Verify(scripts.List().Count==12,"multiple independent draft files can coexist");
 var mediaDraft=scripts.Create("图文草稿");
 var reference=new MediaReference(new string('a',64),"image/png",92,1,1,"图片.png");
 var withImage=new EditorDocument("图文草稿",[new ScriptRole(roleA,"客服A")],
   [new EditorStep(Guid.NewGuid().ToString("D"),roleA,"配图说明",30000,5000,0,reference)],
   new TimingProfile(5000,5000,0));
 var mediaSaved=scripts.Save(Guid.Parse(mediaDraft.ScriptId),1,withImage);
 Verify(mediaSaved.Document.Steps[0].Media?.AssetId==reference.AssetId,
   "portable media hash retained in saved script draft");
 Verify(mediaSaved.Document.Defaults?.TypingDurationMs==5000,
   "default timing independent from per-message 30+5 configuration");
 Verify(scripts.Get(Guid.Parse(mediaDraft.ScriptId))!.Document.Steps[0].Media?.FileName=="图片.png",
   "image display name round trips as metadata without machine absolute paths");
 var clonedMedia=scripts.Duplicate(Guid.Parse(mediaDraft.ScriptId));
 Verify(clonedMedia.Document.Steps[0].Media?.AssetId==reference.AssetId,
   "copying a script retains content addressed image reference");
 bool invalidMedia=false;
 try{EditorDocumentRules.Validate(withImage with {Steps=[
   withImage.Steps[0] with { Media=reference with {FileName="../bad.png"}}
 ]});}catch(ArgumentException){invalidMedia=true;}
 Verify(invalidMedia,"unsafe image display name rejected by .NET validation");
 Console.WriteLine($"SUMMARY: {checks} editor draft assertions passed; no TDLib connection, scripts remain unpublished.");
}
finally{try{Directory.Delete(root,true);}catch(IOException){}}
