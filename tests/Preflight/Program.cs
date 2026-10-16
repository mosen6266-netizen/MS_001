using Mosen.GroupScheduler.Persistence;
using Mosen.GroupScheduler.Preflight;
using Mosen.GroupScheduler.Groups;
using Mosen.GroupScheduler.Scripts.Editor;
using Mosen.GroupScheduler.Scripts.Media;
using Mosen.GroupScheduler.Scripts.Preview;
using Microsoft.Data.Sqlite;
int count=0;
void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);count++;Console.WriteLine("PASS "+name);}
void Reject(Action action,string label)
{bool denied=false;try{action();}catch(Exception e)when(e is ArgumentException or InvalidOperationException or DraftConflictException or InvalidDataException or IOException){denied=true;}Check(denied,label);}
var root=Path.Combine(Path.GetTempPath(),"ms001-preflight-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var db=new AppDatabase(Path.Combine(root,"app.db"));
    var media=new MediaAssetStore(Path.Combine(root,"assets"));
    var draft=new EditorDraftRepository(db);
    var publish=new PublishedScriptRepository(db,media);
    var empty=draft.Create("不可发布空剧本");
    Reject(()=>publish.Publish(Guid.Parse(empty.ScriptId),empty.Revision),"empty scripts cannot publish");
    var one=Guid.NewGuid().ToString("D");
    var two=Guid.NewGuid().ToString("D");
    var a=Guid.NewGuid().ToString("D");
    var b=Guid.NewGuid().ToString("D");
    var doc=new EditorDocument("公告",
      new(){new(one,"编辑A"),new(two,"编辑B")},
      new(){new(a,one,"公告 https://example.com/a",30000,5000,12000),
            new(b,two,"第二条",1500,500,0)});
    var v1=draft.CreateDocument(doc);
    var id=Guid.Parse(v1.ScriptId);
    var pub=publish.Publish(id,v1.Revision);
    Check(pub.Version==1&&pub.SourceRevision==1,"first publish freezes revision 1");
    Check(publish.Get(id,1)!.Sha256==pub.Sha256,"published hash stable on reload");
    var preview=ScriptPreviewBuilder.Build(pub);
    Check(preview.Entries[0].EarliestSendFromStepStartMs==35000,"wait 30s then type 5s before send");
    Check(preview.Entries[0].PostSendMs==12000&&preview.Entries[1].WaitingMs==1500,
      "post-wait and legacy millisecond values retained");
    Check(preview.Entries[0].Links.Count==1&&preview.Entries[0].Links[0].Url=="https://example.com/a",
      "link entity offset and URL detected");
    var edit=draft.Save(id,v1.Revision,doc with {Steps=new(){doc.Steps[0] with {Body="修改后的草稿"},doc.Steps[1]}});
    Check(publish.Get(id,1)!.Document.Steps[0].Body==doc.Steps[0].Body,
      "editing draft cannot mutate published version 1");
    Reject(()=>publish.Publish(id,v1.Revision),"stale revision cannot publish");
    var pub2=publish.Publish(id,edit.Revision);
    Check(pub2.Version==2&&pub2.SourceRevision==edit.Revision,"second published version increments independently");
    Check(publish.Get(id,1)!.Sha256==pub.Sha256&&publish.Get(id,2)!.Sha256!=pub.Sha256,
      "both published versions stay immutable");
    var now=DateTimeOffset.UtcNow;
    var accA=Guid.NewGuid();var accB=Guid.NewGuid();var accC=Guid.NewGuid();
    PreflightAccount Account(Guid id,string remark,bool authorized=true,bool photo=true,int slow=0)
       =>new(id,remark,authorized,new MembershipInfo(id,"-100123","member",true,photo,slow,now));
    var group=new GroupInfo("-100123","测试群","supergroup",false,now);
    PreflightTarget Target(IReadOnlyList<PreflightAccount> accounts,bool refreshed=true,bool conflicting=false)
       =>new("-100123",null,group,null,accounts,refreshed,conflicting);
    var good=Target(new[]{Account(accA,"编辑A"),Account(accB,"编辑B"),Account(accC,"额外账号")});
    var ok=PreflightPlanner.Check(pub,new[]{good},Array.Empty<RoleChoice>(),_=>true,now);
    Check(ok.Ready&&ok.FrozenTargets.Count==1&&ok.FrozenTargets[0].Bindings.Count==2,
      "group-scoped exactly matched remarks; extra account ignored");
    Check(ok.FrozenTargets[0].Steps[0].AccountId==accA&&ok.FrozenTargets[0].Steps[0].PreSendDelayMs==30000,
      "frozen bindings preserve account selection and serial timing");
    var changed=draft.Save(id,edit.Revision,doc with {Title="任意新草稿",Steps=new(){doc.Steps[0] with {Body="变化"},doc.Steps[1]}});
    Check(ok.FrozenTargets[0].Steps[0].Body=="公告 https://example.com/a",
      "later draft edits cannot mutate preflight frozen plan");
    var missing=PreflightPlanner.Check(pub,new[]{Target(new[]{Account(accC,"不相关")})},
       Array.Empty<RoleChoice>(),_=>true,now);
    Check(!missing.Ready&&missing.Issues.Count(x=>x.Code=="ROLE_MISSING")==2,
      "all missing roles listed, never silently ignored");
    var ambiguity=PreflightPlanner.Check(pub,new[]{Target(new[]{Account(accA,"编辑A"),Account(accC,"编辑A"),Account(accB,"编辑B")})},
       Array.Empty<RoleChoice>(),_=>true,now);
    Check(!ambiguity.Ready&&ambiguity.Issues.Any(x=>x.Code=="ROLE_AMBIGUOUS"),
      "two permitted same-remark accounts require explicit selection");
    var choices=new[]{new RoleChoice("-100123",null,one,accC)};
    var picked=PreflightPlanner.Check(pub,new[]{Target(new[]{Account(accA,"编辑A"),Account(accC,"编辑A"),Account(accB,"编辑B")})},
        choices,_=>true,now);
    Check(picked.Ready&&picked.FrozenTargets[0].Bindings.Single(x=>x.RoleKey==one).AccountId==accC,
      "explicit group-scoped choice freezes correct account");
    Check(!PreflightPlanner.Check(pub,new[]{good},new[]{new RoleChoice("-100123",null,one,Guid.NewGuid())},_=>true,now).Ready,
      "selection cannot target unverified account");
    var unauthorized=PreflightPlanner.Check(pub,new[]{Target(new[]{Account(accA,"编辑A",false),Account(accB,"编辑B",false)})},
       Array.Empty<RoleChoice>(),_=>true,now);
    Check(!unauthorized.Ready&&unauthorized.Issues.Count(x=>x.Code=="ACCOUNT_UNAVAILABLE")==2,
       "offline or unverified login blocks start");
    var stale=PreflightPlanner.Check(pub,new[]{Target(new[]{Account(accA,"编辑A"),Account(accB,"编辑B")},false)},
       Array.Empty<RoleChoice>(),_=>true,now);
    Check(!stale.Ready&&stale.Issues.Any(x=>x.Code=="ACCOUNT_UNAVAILABLE"),
       "permissions not checked during this start cannot authorize a run");
    var conflicted=PreflightPlanner.Check(pub,new[]{Target(new[]{Account(accA,"编辑A"),Account(accB,"编辑B")},true,true)},
       Array.Empty<RoleChoice>(),_=>true,now);
    Check(!conflicted.Ready&&conflicted.Issues.Any(x=>x.Code=="TASK_CONFLICT"),"active group task blocks launch");
    var badmedia=PreflightPlanner.Check(pub,new[]{good},Array.Empty<RoleChoice>(),_=>false,now);
    Check(badmedia.Ready,"text-only scripts need no images");
    var forum=new GroupInfo("-100123","论坛群","supergroup",true,now);
    var topicNeeded=PreflightPlanner.Check(pub,new[]{good with {Group=forum}},Array.Empty<RoleChoice>(),_=>true,now);
    Check(!topicNeeded.Ready&&topicNeeded.Issues.Any(x=>x.Code=="TOPIC_REQUIRED"),"forum requires a topic");
    var closedTopic=new TopicInfo("-100123","18","议题",true,false,now);
    var invalidTopic=PreflightPlanner.Check(pub,new[]{good with {TopicId="18",Topic=closedTopic}},
       Array.Empty<RoleChoice>(),_=>true,now);
    Check(!invalidTopic.Ready&&invalidTopic.Issues.Any(x=>x.Code=="TOPIC_UNAVAILABLE"),"closed topic blocks launch");
    var duplicate=PreflightPlanner.Check(pub,new[]{good,good},Array.Empty<RoleChoice>(),_=>true,now);
    Check(!duplicate.Ready&&duplicate.Issues.Any(x=>x.Code=="DUPLICATE_TARGET"),
      "duplicate target is flagged");
    Reject(()=>PreflightPlanner.Check(pub,Array.Empty<PreflightTarget>(),Array.Empty<RoleChoice>(),_=>true,now),
      "no target cannot start");
    Reject(()=>PreflightPlanner.Check(pub,Enumerable.Repeat(good,51).ToArray(),
      Array.Empty<RoleChoice>(),_=>true,now),"more than 50 targets rejected");
    // Stored publication cannot be mutated through draft APIs or bypassed via tampered hash.
    using(var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=db.FilePath}.ToString()))
    {
        c.Open();using var sql=c.CreateCommand();
        sql.CommandText="UPDATE published_script_snapshots SET sha256='0000000000000000000000000000000000000000000000000000000000000000' WHERE script_id=$id AND version=1;";
        sql.Parameters.AddWithValue("$id",id.ToString("D"));sql.ExecuteNonQuery();
    }
    Reject(()=>publish.Get(id,1),"published snapshot hash tamper detected");
    // A script with corrupt referenced local media cannot be published.
    var png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL/nwAAAABJRU5ErkJggg==");
    var image=media.ImportBase64("图.png","image/png",Convert.ToBase64String(png));
    var imageDoc=doc with {Steps=new(){doc.Steps[0] with {Media=image},doc.Steps[1]}};
    var imageDraft=draft.CreateDocument(imageDoc);
    var imgPublished=publish.Publish(Guid.Parse(imageDraft.ScriptId),imageDraft.Revision);
    Check(imgPublished.Document.Steps[0].Media?.AssetId==image.AssetId,"published image metadata frozen");
    var error=PreflightPlanner.Check(imgPublished,new[]{good},Array.Empty<RoleChoice>(),_=>false,now);
    Check(!error.Ready&&error.Issues.Any(x=>x.Code=="MEDIA_INVALID"),"media missing or damaged blocks start");
    File.WriteAllBytes(Directory.GetFiles(media.RootPath).Single(),new byte[48]);
    Reject(()=>publish.Publish(Guid.Parse(imageDraft.ScriptId),imageDraft.Revision),"publishing corrupt local media blocked");
    Console.WriteLine($"SUMMARY: {count} Step16 checks passed. All group and accounts in these tests are synthetic.");
}
finally {try{Directory.Delete(root,true);}catch(IOException){}}
