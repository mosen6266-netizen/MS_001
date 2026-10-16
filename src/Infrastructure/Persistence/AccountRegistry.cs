using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Mosen.GroupScheduler.Persistence;

/// <summary>Local account metadata only; no phone numbers, auth tokens or TDLib databases.</summary>
public sealed record AccountEntry(string Id,string Alias,string GroupTag,string DisplayName,
    long Revision,string? VerifiedUserId,bool Removed)
{
    public bool CanAttachSession => Guid.TryParseExact(Id,"D",out var id) && id!=Guid.Empty;
}
public sealed record AccountPage(IReadOnlyList<AccountEntry> Items,int Total,int Page,int PageSize);
public sealed class AccountVersionConflict : Exception
{
    public AccountVersionConflict():base("Account metadata was updated elsewhere; reload before editing."){}
}
public sealed class AccountRegistry
{
    private const int MaxEntries=300;
    private readonly AppDatabase _database;
    public AccountRegistry(AppDatabase database)=>_database=database ?? throw new ArgumentNullException(nameof(database));
    private SqliteConnection Open()
    {
        _database.Initialize();
        var c=new SqliteConnection(new SqliteConnectionStringBuilder{
            DataSource=_database.FilePath,Mode=SqliteOpenMode.ReadWrite,
            ForeignKeys=true,Cache=SqliteCacheMode.Private,Pooling=false,DefaultTimeout=10}.ToString());
        c.Open();
        using var pragma=c.CreateCommand();
        pragma.CommandText="PRAGMA busy_timeout=10000;PRAGMA foreign_keys=ON;";
        pragma.ExecuteNonQuery();
        return c;
    }
    private static void ValidateLabel(string value,string name,bool emptyAllowed=false)
    {
        if(value is null || value.Length>64 || value.Any(char.IsControl) ||
            (!emptyAllowed && string.IsNullOrWhiteSpace(value)))
            throw new ArgumentException("Invalid local account metadata.",name);
    }
    private static void ValidateId(Guid id)
    {
        if(id==Guid.Empty)throw new ArgumentException("Invalid account ID.",nameof(id));
    }
    public AccountEntry Register(string alias,string groupTag="")
    {
        ValidateLabel(alias,nameof(alias));ValidateLabel(groupTag,nameof(groupTag),true);
        using var c=Open();
        using var tx=c.BeginTransaction(deferred:false);
        using var check=c.CreateCommand();
        check.Transaction=tx;
        check.CommandText="SELECT COUNT(*) FROM accounts WHERE deleted_utc IS NULL;";
        if(Convert.ToInt32(check.ExecuteScalar(),CultureInfo.InvariantCulture)>=MaxEntries)
            throw new InvalidOperationException("Account metadata limit (300) reached.");
        Guid id=Guid.NewGuid();
        string now=DateTimeOffset.UtcNow.ToString("O",CultureInfo.InvariantCulture);
        using var insert=c.CreateCommand();
        insert.Transaction=tx;
        insert.CommandText=@"INSERT INTO accounts(account_id,alias,group_tag,created_utc,updated_utc,login_state)
                              VALUES($id,$alias,$tag,$now,$now,'disconnected');";
        insert.Parameters.AddWithValue("$id",id.ToString("D"));
        insert.Parameters.AddWithValue("$alias",alias.Trim());
        insert.Parameters.AddWithValue("$tag",groupTag.Trim());
        insert.Parameters.AddWithValue("$now",now);
        insert.ExecuteNonQuery();
        tx.Commit();
        return new(id.ToString("D"),alias.Trim(),groupTag.Trim(),"",1,null,false);
    }
    public AccountEntry? Get(Guid id,bool includeRemoved=false)
    {
        ValidateId(id);
        using var c=Open();
        using var cmd=c.CreateCommand();
        cmd.CommandText=@"SELECT account_id,alias,group_tag,display_name,revision,verified_telegram_user_id,deleted_utc
                            FROM accounts WHERE account_id=$id;";
        cmd.Parameters.AddWithValue("$id",id.ToString("D"));
        using var rows=cmd.ExecuteReader();
        if(!rows.Read())return null;
        AccountEntry entry=Map(rows);
        return entry.Removed&&!includeRemoved?null:entry;
    }
    public AccountPage Find(string query="",string? group=null,int page=1,int pageSize=20)
    {
        if(page<1 || pageSize is not (10 or 20 or 50))throw new ArgumentOutOfRangeException(nameof(pageSize));
        if(query is null || query.Length>128)throw new ArgumentException("Invalid search term.",nameof(query));
        if(group is not null)ValidateLabel(group,nameof(group),true);
        using var c=Open();
        using var cmd=c.CreateCommand();
        cmd.CommandText=@"SELECT account_id,alias,group_tag,display_name,revision,verified_telegram_user_id,deleted_utc
                            FROM accounts WHERE deleted_utc IS NULL ORDER BY created_utc,account_id;";
        using var rows=cmd.ExecuteReader();
        var all=new List<AccountEntry>(MaxEntries);
        while(rows.Read())
        {
            var item=Map(rows);
            if(group is not null && item.GroupTag!=group)continue;
            if(query.Length>0 && !item.Alias.Contains(query,StringComparison.OrdinalIgnoreCase) &&
                !item.DisplayName.Contains(query,StringComparison.OrdinalIgnoreCase))continue;
            all.Add(item);
        }
        int total=all.Count;
        return new(all.Skip(((long)page-1)*pageSize>int.MaxValue?int.MaxValue:(page-1)*pageSize).Take(pageSize).ToArray(),
            total,page,pageSize);
    }
    public AccountEntry Edit(Guid id,long expectedRevision,string alias,string groupTag)
    {
        ValidateId(id);ValidateLabel(alias,nameof(alias));ValidateLabel(groupTag,nameof(groupTag),true);
        using var c=Open();
        using var cmd=c.CreateCommand();
        cmd.CommandText=@"UPDATE accounts SET alias=$alias,group_tag=$tag,revision=revision+1,updated_utc=$now
                            WHERE account_id=$id AND revision=$revision AND deleted_utc IS NULL;";
        cmd.Parameters.AddWithValue("$alias",alias.Trim());cmd.Parameters.AddWithValue("$tag",groupTag.Trim());
        cmd.Parameters.AddWithValue("$id",id.ToString("D"));cmd.Parameters.AddWithValue("$revision",expectedRevision);
        cmd.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));
        if(expectedRevision<=0 || cmd.ExecuteNonQuery()!=1)throw new AccountVersionConflict();
        return Get(id)!;
    }
    /// <summary>Call only from trusted TDLib getMe response after official authorizationReady.</summary>
    public AccountEntry BindVerifiedIdentity(Guid id,long expectedRevision,long telegramUserId,string displayName)
    {
        ValidateId(id);ValidateLabel(displayName,nameof(displayName),true);
        if(telegramUserId<=0)throw new ArgumentOutOfRangeException(nameof(telegramUserId));
        using var c=Open();using var cmd=c.CreateCommand();
        cmd.CommandText=@"UPDATE accounts SET verified_telegram_user_id=$uid,display_name=$display,
                            revision=revision+1,updated_utc=$now WHERE account_id=$id AND revision=$rev AND deleted_utc IS NULL;";
        cmd.Parameters.AddWithValue("$uid",telegramUserId.ToString(CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$display",displayName);cmd.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$id",id.ToString("D"));cmd.Parameters.AddWithValue("$rev",expectedRevision);
        try{if(cmd.ExecuteNonQuery()!=1)throw new AccountVersionConflict();}
        catch(SqliteException e) when(e.SqliteErrorCode==19){throw new InvalidOperationException("This Telegram user is already registered in another local slot.");}
        return Get(id)!;
    }
    /// <summary>Two-part confirmation, archive only. No TDLib directory or keys removed automatically.</summary>
    public void Archive(Guid id,long expectedRevision,string typedAlias)
    {
        ValidateId(id);ValidateLabel(typedAlias,nameof(typedAlias));
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        using var check=c.CreateCommand();check.Transaction=tx;
        check.CommandText="SELECT alias,revision,deleted_utc FROM accounts WHERE account_id=$id;";
        check.Parameters.AddWithValue("$id",id.ToString("D"));
        using(var rows=check.ExecuteReader())
        {
            if(!rows.Read() || !rows.IsDBNull(2) || rows.GetInt64(1)!=expectedRevision)
                throw new AccountVersionConflict();
            if(!string.Equals(rows.GetString(0),typedAlias,StringComparison.Ordinal))
                throw new InvalidOperationException("Confirmation remark does not match.");
        }
        using var dependencies=c.CreateCommand();dependencies.Transaction=tx;
        dependencies.CommandText=@"SELECT (SELECT COUNT(*) FROM group_bindings WHERE account_id=$id) +
                                         (SELECT COUNT(*) FROM run_steps WHERE account_id=$id);";
        dependencies.Parameters.AddWithValue("$id",id.ToString("D"));
        if(Convert.ToInt64(dependencies.ExecuteScalar())>0)
            throw new InvalidOperationException("Account is referenced by a group binding or task; resolve dependencies first.");
        using var cmd=c.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText=@"UPDATE accounts SET deleted_utc=$now,login_state='restricted',revision=revision+1,
                            updated_utc=$now WHERE account_id=$id AND revision=$rev AND deleted_utc IS NULL;";
        cmd.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$id",id.ToString("D"));cmd.Parameters.AddWithValue("$rev",expectedRevision);
        if(cmd.ExecuteNonQuery()!=1)throw new AccountVersionConflict();
        tx.Commit();
    }
    public void Restore(Guid id,long expectedRevision)
    {
        ValidateId(id);
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        using(var limit=c.CreateCommand())
        {
            limit.Transaction=tx;limit.CommandText="SELECT COUNT(*) FROM accounts WHERE deleted_utc IS NULL;";
            if(Convert.ToInt32(limit.ExecuteScalar())>=MaxEntries)throw new InvalidOperationException("Account limit reached.");
        }
        using var cmd=c.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText=@"UPDATE accounts SET deleted_utc=NULL,login_state='auth_required',revision=revision+1,
                            updated_utc=$now WHERE account_id=$id AND revision=$rev AND deleted_utc IS NOT NULL;";
        cmd.Parameters.AddWithValue("$id",id.ToString("D"));cmd.Parameters.AddWithValue("$rev",expectedRevision);
        cmd.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));
        if(cmd.ExecuteNonQuery()!=1)throw new AccountVersionConflict();
        tx.Commit();
    }
    private static AccountEntry Map(SqliteDataReader r)
    {
        return new(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetInt64(4),
            r.IsDBNull(5)?null:r.GetString(5),!r.IsDBNull(6));
    }
}
