using System.Globalization;
using Microsoft.Data.Sqlite;
using Mosen.GroupScheduler.Persistence;

namespace Mosen.GroupScheduler.Groups;

/// <summary>
/// Telegram-origin catalog: only the trusted TDLib coordinator may call Upsert.
/// Not accessible via any WebView2 write IPC. No phone, session key or message body.
/// </summary>
public sealed class GroupCatalogRepository
{
    private readonly AppDatabase _db;
    public GroupCatalogRepository(AppDatabase db)=>_db=db??throw new ArgumentNullException(nameof(db));
    private SqliteConnection Open()
    {
        _db.Initialize();
        var connection=new SqliteConnection(new SqliteConnectionStringBuilder{
            DataSource=_db.FilePath,Mode=SqliteOpenMode.ReadWrite,Pooling=false,
            Cache=SqliteCacheMode.Private,ForeignKeys=true,DefaultTimeout=10
        }.ToString());
        connection.Open();
        using var cmd=connection.CreateCommand();
        cmd.CommandText="PRAGMA busy_timeout=10000; PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();
        return connection;
    }
    private static void ValidateGroup(GroupInfo g)
    {
        if(!long.TryParse(g.ChatId,NumberStyles.Integer,CultureInfo.InvariantCulture,out long n)||n==0 ||
            g.Kind is not ("group" or "supergroup" or "channel") ||
            string.IsNullOrWhiteSpace(g.Title)||g.Title.Length>256||g.Title.Any(char.IsControl))
            throw new ArgumentException("Untrusted group metadata.");
    }
    private static string Time(DateTimeOffset timestamp)=>timestamp.ToUniversalTime().ToString("O",CultureInfo.InvariantCulture);
    private static DateTimeOffset Parsed(string time)=>DateTimeOffset.Parse(time,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind);
    private static bool Bool(SqliteDataReader r,int i)=>r.GetInt64(i)!=0;
    public void UpsertChat(GroupInfo item)
    {
        ArgumentNullException.ThrowIfNull(item);ValidateGroup(item);
        using var c=Open();using var cmd=c.CreateCommand();
        cmd.CommandText=@"INSERT INTO chats(chat_id,title,chat_kind,updated_utc,source,is_forum)
                           VALUES($id,$title,$kind,$utc,'tdlib',$forum)
                           ON CONFLICT(chat_id) DO UPDATE SET title=excluded.title,
                           chat_kind=excluded.chat_kind,updated_utc=excluded.updated_utc,
                           source='tdlib',is_forum=excluded.is_forum
                           WHERE chats.updated_utc <= excluded.updated_utc;";
        cmd.Parameters.AddWithValue("$id",item.ChatId);cmd.Parameters.AddWithValue("$title",item.Title);
        cmd.Parameters.AddWithValue("$kind",item.Kind);cmd.Parameters.AddWithValue("$utc",Time(item.CheckedUtc));
        cmd.Parameters.AddWithValue("$forum",item.IsForum?1:0);
        cmd.ExecuteNonQuery();
    }
    public void UpsertTopic(TopicInfo item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if(!long.TryParse(item.ChatId,out _)||!long.TryParse(item.TopicId,out var topicId)||topicId<=0||
           string.IsNullOrWhiteSpace(item.Title)||item.Title.Length>256||item.Title.Any(char.IsControl))
            throw new ArgumentException("Invalid forum topic.");
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        using(var verify=c.CreateCommand())
        {
            verify.Transaction=tx;
            verify.CommandText="SELECT COUNT(*) FROM chats WHERE chat_id=$id AND source='tdlib' AND chat_kind='supergroup';";
            verify.Parameters.AddWithValue("$id",item.ChatId);
            if(Convert.ToInt64(verify.ExecuteScalar())!=1)throw new InvalidOperationException("Topic chat not verified.");
        }
        using var cmd=c.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText=@"INSERT INTO topics(chat_id,topic_id,title,is_closed,is_hidden,updated_utc)
                           VALUES($chat,$topic,$title,$closed,$hidden,$utc)
                           ON CONFLICT(chat_id,topic_id) DO UPDATE SET title=excluded.title,
                           is_closed=excluded.is_closed,is_hidden=excluded.is_hidden,updated_utc=excluded.updated_utc
                           WHERE topics.updated_utc <= excluded.updated_utc;";
        cmd.Parameters.AddWithValue("$chat",item.ChatId);cmd.Parameters.AddWithValue("$topic",item.TopicId);
        cmd.Parameters.AddWithValue("$title",item.Title);cmd.Parameters.AddWithValue("$closed",item.IsClosed?1:0);
        cmd.Parameters.AddWithValue("$hidden",item.IsHidden?1:0);cmd.Parameters.AddWithValue("$utc",Time(item.CheckedUtc));
        cmd.ExecuteNonQuery();tx.Commit();
    }
    public void UpsertMembership(MembershipInfo item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if(item.AccountId==Guid.Empty||item.Status is not ("unknown" or "member" or "administrator" or "creator" or "restricted" or "left" or "banned")||
           item.SlowModeDelaySeconds is <0 or >3600)throw new ArgumentException("Untrusted membership snapshot.");
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        using(var check=c.CreateCommand())
        {
            check.Transaction=tx;
            check.CommandText=@"SELECT (SELECT COUNT(*) FROM accounts WHERE account_id=$account AND deleted_utc IS NULL
                                  AND verified_telegram_user_id IS NOT NULL) AS verified_account,
                                 (SELECT COUNT(*) FROM chats WHERE chat_id=$chat AND source='tdlib') AS verified_chat;";
            check.Parameters.AddWithValue("$account",item.AccountId.ToString("D"));
            check.Parameters.AddWithValue("$chat",item.ChatId);
            using var r=check.ExecuteReader();
            if(!r.Read()||r.GetInt64(0)!=1||r.GetInt64(1)!=1)
                throw new InvalidOperationException("Cannot attach membership without an officially verified account and TDLib chat.");
        }
        using var cmd=c.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText=@"INSERT INTO account_memberships(
                           account_id,chat_id,can_send,checked_utc,member_status,can_send_text,
                           can_send_photos,slow_mode_delay_seconds,permission_source)
                           VALUES($account,$chat,$can,$utc,$status,$text,$photos,$delay,'tdlib')
                           ON CONFLICT(account_id,chat_id) DO UPDATE SET
                           can_send=excluded.can_send,checked_utc=excluded.checked_utc,
                           member_status=excluded.member_status,can_send_text=excluded.can_send_text,
                           can_send_photos=excluded.can_send_photos,
                           slow_mode_delay_seconds=excluded.slow_mode_delay_seconds,permission_source='tdlib'
                           WHERE account_memberships.checked_utc <= excluded.checked_utc;";
        cmd.Parameters.AddWithValue("$account",item.AccountId.ToString("D"));
        cmd.Parameters.AddWithValue("$chat",item.ChatId);
        cmd.Parameters.AddWithValue("$can",item.CanSendText?1:0);
        cmd.Parameters.AddWithValue("$utc",Time(item.CheckedUtc));
        cmd.Parameters.AddWithValue("$status",item.Status);
        cmd.Parameters.AddWithValue("$text",item.CanSendText?1:0);
        cmd.Parameters.AddWithValue("$photos",item.CanSendPhotos?1:0);
        cmd.Parameters.AddWithValue("$delay",item.SlowModeDelaySeconds);
        cmd.ExecuteNonQuery();tx.Commit();
    }
    public GroupInfo? GetChat(string id)
    {
        using var c=Open();using var cmd=c.CreateCommand();
        cmd.CommandText=@"SELECT chat_id,title,chat_kind,is_forum,updated_utc
                           FROM chats WHERE source='tdlib' AND chat_id=$id;";
        cmd.Parameters.AddWithValue("$id",id);
        using var r=cmd.ExecuteReader();
        return r.Read()?new(r.GetString(0),r.GetString(1),r.GetString(2),Bool(r,3),Parsed(r.GetString(4))):null;
    }
    public TopicInfo? GetTopic(string chatId,string topicId)
    {
        using var c=Open();using var cmd=c.CreateCommand();
        cmd.CommandText=@"SELECT t.chat_id,t.topic_id,t.title,t.is_closed,t.is_hidden,t.updated_utc
                          FROM topics t JOIN chats c ON t.chat_id=c.chat_id
                          WHERE c.source='tdlib' AND t.chat_id=$chat AND t.topic_id=$topic;";
        cmd.Parameters.AddWithValue("$chat",chatId);cmd.Parameters.AddWithValue("$topic",topicId);
        using var r=cmd.ExecuteReader();
        return r.Read()?new(r.GetString(0),r.GetString(1),r.GetString(2),Bool(r,3),Bool(r,4),Parsed(r.GetString(5))):null;
    }
    public MembershipInfo? GetMembership(Guid accountId,string chatId)
    {
        using var c=Open();using var cmd=c.CreateCommand();
        cmd.CommandText=@"SELECT m.account_id,m.chat_id,m.member_status,m.can_send_text,m.can_send_photos,
                                 m.slow_mode_delay_seconds,m.checked_utc
                          FROM account_memberships m JOIN chats c ON m.chat_id=c.chat_id
                          JOIN accounts a ON m.account_id=a.account_id
                          WHERE m.account_id=$account AND m.chat_id=$chat AND m.permission_source='tdlib'
                            AND c.source='tdlib' AND a.deleted_utc IS NULL;";
        cmd.Parameters.AddWithValue("$account",accountId.ToString("D"));cmd.Parameters.AddWithValue("$chat",chatId);
        using var r=cmd.ExecuteReader();
        return r.Read()?new(accountId,r.GetString(1),r.GetString(2),Bool(r,3),Bool(r,4),r.GetInt32(5),Parsed(r.GetString(6))):null;
    }
    public GroupPage FindGroups(string query="",int page=1,int pageSize=20)
    {
        if(page<1||page>1000000||pageSize is not (10 or 20 or 50)||query is null||query.Length>128||query.Any(char.IsControl))
            throw new ArgumentException("Invalid catalog paging/filter.");
        using var c=Open();using var cmd=c.CreateCommand();
        cmd.CommandText=@"SELECT chat_id,title,chat_kind,is_forum,updated_utc FROM chats
                          WHERE source='tdlib' AND chat_kind IN ('group','supergroup')
                          ORDER BY title COLLATE NOCASE,chat_id;";
        using var r=cmd.ExecuteReader();var all=new List<GroupInfo>();
        while(r.Read())
        {
            var g=new GroupInfo(r.GetString(0),r.GetString(1),r.GetString(2),Bool(r,3),Parsed(r.GetString(4)));
            if(query.Length==0||g.Title.Contains(query,StringComparison.OrdinalIgnoreCase))all.Add(g);
        }
        int skip=page>int.MaxValue/pageSize?int.MaxValue:(page-1)*pageSize;
        return new(all.Skip(skip).Take(pageSize).ToArray(),all.Count,page,pageSize);
    }
    public TopicPage FindTopics(string chatId,int page=1,int pageSize=20)
    {
        if(page<1||page>1000000||pageSize is not (10 or 20 or 50))
            throw new ArgumentException("Invalid topic paging.");
        using var c=Open();using var cmd=c.CreateCommand();
        cmd.CommandText=@"SELECT t.chat_id,t.topic_id,t.title,t.is_closed,t.is_hidden,t.updated_utc
                          FROM topics t JOIN chats c ON t.chat_id=c.chat_id
                          WHERE t.chat_id=$id AND c.source='tdlib' ORDER BY t.topic_id;";
        cmd.Parameters.AddWithValue("$id",chatId);
        using var r=cmd.ExecuteReader();var all=new List<TopicInfo>();
        while(r.Read())all.Add(new(r.GetString(0),r.GetString(1),r.GetString(2),Bool(r,3),Bool(r,4),Parsed(r.GetString(5))));
        int skip=page>int.MaxValue/pageSize?int.MaxValue:(page-1)*pageSize;
        return new(all.Skip(skip).Take(pageSize).ToArray(),all.Count,page,pageSize);
    }
    public IReadOnlyList<MembershipDisplay> FindMembers(string chatId)
    {
        using var c=Open();using var cmd=c.CreateCommand();
        cmd.CommandText=@"SELECT a.account_id,a.alias,m.member_status,m.can_send_text,m.can_send_photos,
                                 m.slow_mode_delay_seconds,m.checked_utc
                          FROM account_memberships m JOIN accounts a ON m.account_id=a.account_id
                          JOIN chats c ON c.chat_id=m.chat_id
                          WHERE m.chat_id=$chat AND m.permission_source='tdlib' AND c.source='tdlib'
                            AND a.deleted_utc IS NULL ORDER BY a.alias LIMIT 300;";
        cmd.Parameters.AddWithValue("$chat",chatId);
        using var r=cmd.ExecuteReader();var result=new List<MembershipDisplay>();
        while(r.Read())result.Add(new(r.GetString(0),r.GetString(1),r.GetString(2),Bool(r,3),Bool(r,4),
            r.GetInt32(5),Parsed(r.GetString(6))));
        return result;
    }
}
