using System.Text.Json;
using Microsoft.Data.Sqlite;
using Mosen.GroupScheduler.Groups;
using Mosen.GroupScheduler.Persistence;
using Mosen.GroupScheduler.Telegram;

int passed=0;
void Assert(bool b,string name)
{
    if(!b)throw new Exception("FAIL: "+name);
    passed++;Console.WriteLine("PASS "+name);
}
TdlibEnvelope Raw(int client,string kind,string json)=>new(client,kind,JsonDocument.Parse(json).RootElement.Clone());
var now=new DateTimeOffset(2026,10,16,10,0,0,TimeSpan.Zero);
var rawChat=Raw(12,"chat", """{"@type":"chat","@client_id":12,"id":-1001234567,"title":"团队协作群","type":{"@type":"chatTypeSupergroup","supergroup_id":777,"is_channel":false},"permissions":{"@type":"chatPermissions","can_send_basic_messages":true,"can_send_photos":true}}""");
var group=TdlibGroupParser.ReadChat(rawChat,now)!;
Assert(group.ChatId=="-1001234567"&&group.Title=="团队协作群"&&group.Kind=="supergroup","official TDLib chat parsed with stable ID");
Assert(TdlibGroupParser.ReadChat(Raw(12,"chat", """{"id":99,"title":"普通私聊","type":{"@type":"chatTypePrivate"}}"""),now)==null,"private chat excluded from group directory");
Assert(TdlibGroupParser.ReadChat(Raw(12,"updateNewChat","""{"chat":{"id":-1001234567,"title":"协作群","type":{"@type":"chatTypeSupergroup","is_channel":false}}}"""),now) is not null,"new-chat update parser accepts official nested chat");
Assert(TdlibGroupParser.ReadChat(Raw(12,"updateChatTitle","""{"chat_id":-1001234567,"title":"变更标题"}"""),now)==null,"partial title update not incorrectly treated as whole chat");
var topic=TdlibGroupParser.ReadTopic(Raw(12,"forumTopicInfo","""{"@type":"forumTopicInfo","message_thread_id":99,"name":"项目进度","is_closed":false,"is_hidden":false}"""),group.ChatId,now)!;
Assert(topic.TopicId=="99"&&topic.Title=="项目进度","forum topic ID and title parsed");
Assert(TdlibGroupParser.ReadTopic(Raw(12,"forumTopicInfo","""{"chat_id":100,"message_thread_id":99,"name":"别的群"}"""),group.ChatId,now)==null,"cross-chat topic payload rejected");
var member=TdlibGroupParser.ReadMembership(Raw(12,"chatMember","""{"member_id":{"@type":"messageSenderUser","user_id":78901234},"status":{"@type":"chatMemberStatusMember"}}"""),Guid.NewGuid(),78901234,group.ChatId,12,now)!;
Assert(TdlibGroupParser.ReadMembership(Raw(12,"chatMember","""{"member_id":{"@type":"messageSenderUser","user_id":777},"status":{"@type":"chatMemberStatusMember"}}"""),Guid.NewGuid(),78901234,group.ChatId,12,now)==null,"member update for another Telegram user rejected");
Assert(member.Status=="member"&&!member.CanSendText&&!member.CanSendPhotos,"member status alone never grants sending permission");
using var d=JsonDocument.Parse("""{"@type":"chatPermissions","can_send_basic_messages":true,"can_send_photos":false}""");
member=TdlibGroupParser.ApplyChatPermissions(member,d.RootElement);
Assert(member.CanSendText&&!member.CanSendPhotos,"text-only membership permission respected");
Assert(TdlibGroupParser.ReadSlowModeSeconds(Raw(12,"updateSupergroupFullInfo","""{"supergroup_id":777,"supergroup_full_info":{"slow_mode_delay":45}}"""))==45,"nested official slow mode delay read correctly");
Assert(TdlibGroupParser.ReadSlowModeSeconds(Raw(12,"supergroupFullInfo","""{"slow_mode_delay":-5}"""))==0,"invalid slow mode cannot be negative");
Assert(TdlibGroupParser.ReadMembership(Raw(12,"chatMember","""{"member_id":{"@type":"messageSenderUser","user_id":78901234},"status":{"@type":"chatMemberStatusBanned"}}"""),Guid.NewGuid(),78901234,group.ChatId,0,now)!.Status=="banned","banned membership state recognized");
Assert(!GroupPermissionPolicy.Preflight(group,null,null,false,now,true).Allowed,"unknown membership always denied");
Assert(GroupPermissionPolicy.Preflight(group,null,member,false,now,false).Code=="RECHECK_REQUIRED","old cache cannot replace start-time official recheck");
Assert(GroupPermissionPolicy.Preflight(group,null,member,false,now.AddMinutes(2),true).Code=="RECHECK_REQUIRED","stale membership not actionable");
Assert(GroupPermissionPolicy.Preflight(group,null,member,true,now,true).Code=="NO_PERMISSION","photos blocked when only text allowed");
Assert(GroupPermissionPolicy.Preflight(group,new(topic.ChatId,topic.TopicId,topic.Title,true,false,now),member,false,now,true).Code=="TOPIC_UNAVAILABLE","closed topic blocked");
Assert(GroupPermissionPolicy.Preflight(group,null,member,false,now,true).Allowed,"fresh verified text membership passes preflight");
Assert(GroupPermissionPolicy.Preflight(group,null,member,false,now,true).SlowModeSeconds==12,"slow mode retained as downstream scheduling constraint");
Assert(!GroupPermissionPolicy.Preflight(group with {Kind="channel"},null,member,false,now,true).Allowed,"channel never masquerades as group");
var banned=member with {Status="banned",CanSendText=true,CanSendPhotos=true};
Assert(GroupPermissionPolicy.Preflight(group,null,banned,false,now,true).Code=="NOT_MEMBER","banned status wins even when contradictory send booleans exist");

string root=Path.Combine(Path.GetTempPath(),"ms001-step12-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
try
{
    string dbPath=Path.Combine(root,"catalog.db");
    var db=new AppDatabase(dbPath);db.Initialize();
    Assert(db.GetSchemaVersion()==8,"metadata schema migrates to version 8");
    var accounts=new AccountRegistry(db);
    var accountA=accounts.Register("角色A");
    var accountB=accounts.Register("角色B");
    var store=new GroupCatalogRepository(db);
    using(var raw=new SqliteConnection("Data Source="+dbPath))
    {
        raw.Open();
        using var cmd=raw.CreateCommand();cmd.CommandText=@"INSERT INTO chats(chat_id,title,chat_kind,updated_utc)
             VALUES('-808080','旧数据手工群','supergroup','2026-10-01T00:00:00.0000000+00:00');";
        cmd.ExecuteNonQuery();
    }
    Assert(store.FindGroups().Total==0,"unverified legacy chat not displayed as Telegram-discovered group");
    bool unverifiedBlocked=false;
    try{store.UpsertMembership(member with {AccountId=Guid.Parse(accountA.Id),ChatId="-808080"});}
    catch(InvalidOperationException){unverifiedBlocked=true;}
    Assert(unverifiedBlocked,"unverified manual chat cannot receive trusted membership");

    store.UpsertChat(group);
    Assert(store.FindGroups().Total==1,"trusted group becomes visible");
    store.UpsertTopic(topic);
    Assert(store.FindTopics(group.ChatId).Total==1,"forum topic stored by stable chat/topic IDs");
    Assert(store.GetTopic(group.ChatId,"99")!.Title=="项目进度","topic lookup by stable ID");

    // Mimic existing script FK group/topic role binding; title rename must never rewrite IDs.
    using(var raw=new SqliteConnection("Data Source="+dbPath))
    {
        raw.Open();using var cmd=raw.CreateCommand();
        cmd.CommandText=@"PRAGMA foreign_keys=ON;
          INSERT INTO scripts(script_id,title,created_utc,updated_utc) VALUES('script-1','公告','2026','2026');
          INSERT INTO script_versions(script_id,version,created_utc) VALUES('script-1',1,'2026');
          INSERT INTO script_roles(script_id,script_version,role_key,role_label) VALUES('script-1',1,'role-a','角色A');
          INSERT INTO group_bindings(binding_id,chat_id,topic_id,script_id,script_version,role_key,account_id,checked_utc)
          VALUES('binding-1','-1001234567','99','script-1',1,'role-a',$account,'2026');";
        cmd.Parameters.AddWithValue("$account",accountA.Id);cmd.ExecuteNonQuery();
    }
    store.UpsertChat(group with {Title="新群名称",CheckedUtc=now.AddMinutes(1)});
    Assert(store.GetChat(group.ChatId)!.Title=="新群名称","renamed chat is persisted with original ID");
    store.UpsertChat(group with {Title="迟到旧标题",CheckedUtc=now.AddSeconds(-1)});
    Assert(store.GetChat(group.ChatId)!.Title=="新群名称","older chat update does not override current title");
    using(var raw=new SqliteConnection("Data Source="+dbPath))
    {
        raw.Open();using var cmd=raw.CreateCommand();
        cmd.CommandText="SELECT chat_id||':'||topic_id FROM group_bindings WHERE binding_id='binding-1';";
        Assert((string)cmd.ExecuteScalar()! == "-1001234567:99","script binding unchanged across chat rename");
    }
    var accountIdA=Guid.Parse(accountA.Id);
    bool noIdentity=false;
    try{store.UpsertMembership(member with {AccountId=accountIdA});}catch(InvalidOperationException){noIdentity=true;}
    Assert(noIdentity,"unverified account identity cannot be given trusted sending permission");
    var verified=accounts.BindVerifiedIdentity(accountIdA,1,78901234,"Test A");
    var memberA=member with {AccountId=accountIdA,CanSendText=true,CanSendPhotos=false};
    store.UpsertMembership(memberA);
    Assert(store.GetMembership(accountIdA,group.ChatId)!.CanSendText,"official identity member permissions saved per account");
    Assert(store.FindMembers(group.ChatId).Count==1,"only verified account membership displayed");
    Assert(store.FindMembers(group.ChatId)[0].Alias==verified.Alias,"membership remains tied to account remark");

    accounts.BindVerifiedIdentity(Guid.Parse(accountB.Id),1,78901235,"Test B");
    var memberB=memberA with {AccountId=Guid.Parse(accountB.Id),CanSendText=false,Status="restricted"};
    store.UpsertMembership(memberB);
    Assert(store.FindMembers(group.ChatId).Count==2,"two account records map to same chat");
    Assert(!store.GetMembership(Guid.Parse(accountB.Id),group.ChatId)!.CanSendText,"account B cannot inherit account A permission");
    store.UpsertMembership(memberA with {CanSendText=false,CheckedUtc=now.AddMinutes(3)});
    Assert(!store.GetMembership(accountIdA,group.ChatId)!.CanSendText,"permission revocation applied immediately");
    store.UpsertMembership(memberA with {CanSendText=true,CheckedUtc=now.AddMinutes(2)});
    Assert(!store.GetMembership(accountIdA,group.ChatId)!.CanSendText,"stale late grant cannot undo later revoke");
    store.UpsertTopic(topic with {Title="已关闭",IsClosed=true,CheckedUtc=now.AddMinutes(4)});
    Assert(store.GetTopic(group.ChatId,"99")!.IsClosed,"topic closure stored");
    store.UpsertTopic(topic with {Title="过期开放",IsClosed=false,CheckedUtc=now.AddMinutes(1)});
    Assert(store.GetTopic(group.ChatId,"99")!.IsClosed,"older topic update cannot reopen closed topic");
    for(int i=0;i<55;i++)store.UpsertChat(new("-"+(9000+i).ToString(),"测试群"+i.ToString("D3"),"group",false,now));
    Assert(store.FindGroups(pageSize:20).Total==56,"56 verified groups indexed by IDs");
    Assert(store.FindGroups(page:2,pageSize:20).Items.Count==20,"stable group pagination");
    Assert(store.FindGroups(query:"测试群001",pageSize:10).Total==1,"Unicode group title search");
    var backup=db.CreateBackup();
    Assert(File.Exists(backup),"SQLite group/topic metadata online backup works");
    var reopened=new GroupCatalogRepository(new AppDatabase(dbPath));
    Assert(reopened.GetChat(group.ChatId)!.Title=="新群名称"&&reopened.GetTopic(group.ChatId,"99")!.IsClosed,
        "restart keeps chat rename and topic closure");
    Assert(reopened.GetMembership(accountIdA,group.ChatId)!.CanSendText==false,"restart retains permission revocation");
    Assert(reopened.FindGroups().Total==56,"group catalog survives restart");
    Console.WriteLine($"SUMMARY: {passed} official-shaped group/topic permission assertions passed; simulated data only, no native TDLib login.");
}
finally{try{Directory.Delete(root,true);}catch(IOException){}}
