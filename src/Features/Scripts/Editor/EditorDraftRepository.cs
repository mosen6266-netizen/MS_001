using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Mosen.GroupScheduler.Persistence;

namespace Mosen.GroupScheduler.Scripts.Editor;

/// <summary>Revision-checked drafts: never modifies published versions, group bindings or outbox.</summary>
public sealed class EditorDraftRepository
{
    private readonly AppDatabase _db;
    public EditorDraftRepository(AppDatabase database) => _db=database??throw new ArgumentNullException(nameof(database));
    private SqliteConnection Open()
    {
        _db.Initialize();
        var c=new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource=_db.FilePath,Mode=SqliteOpenMode.ReadWrite,ForeignKeys=true,
            Cache=SqliteCacheMode.Private,Pooling=false,DefaultTimeout=10
        }.ToString());
        c.Open();using var cmd=c.CreateCommand();
        cmd.CommandText="PRAGMA foreign_keys=ON;PRAGMA busy_timeout=10000;";
        cmd.ExecuteNonQuery();
        return c;
    }
    private static string Time()=>DateTimeOffset.UtcNow.ToString("O",CultureInfo.InvariantCulture);
    private static string Id(Guid id)
    {
        if(id==Guid.Empty)throw new ArgumentException("Missing draft ID.");
        return id.ToString("D");
    }
    public DraftSnapshot Create(string title)
    {
        var doc=EditorDocumentRules.Validate(new EditorDocument(title,new(),new()));
        var id=Guid.NewGuid();
        var now=Time();
        var json=JsonSerializer.Serialize(doc,EditorDocumentRules.WireOptions);
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        using(var count=c.CreateCommand())
        {
            count.Transaction=tx;count.CommandText="SELECT COUNT(*) FROM editor_drafts WHERE archived_utc IS NULL;";
            if(Convert.ToInt32(count.ExecuteScalar())>=200)throw new InvalidOperationException("Too many active script drafts.");
        }
        using(var cmd=c.CreateCommand())
        {
            cmd.Transaction=tx;
            cmd.CommandText="INSERT INTO scripts(script_id,title,created_utc,updated_utc) VALUES($id,$title,$now,$now);";
            cmd.Parameters.AddWithValue("$id",Id(id));cmd.Parameters.AddWithValue("$title",doc.Title);
            cmd.Parameters.AddWithValue("$now",now);cmd.ExecuteNonQuery();
        }
        using(var cmd=c.CreateCommand())
        {
            cmd.Transaction=tx;
            cmd.CommandText=@"INSERT INTO editor_drafts(script_id,revision,snapshot_json,updated_utc)
                              VALUES($id,1,$doc,$now);";
            cmd.Parameters.AddWithValue("$id",Id(id));cmd.Parameters.AddWithValue("$doc",json);
            cmd.Parameters.AddWithValue("$now",now);cmd.ExecuteNonQuery();
        }
        tx.Commit();return new(Id(id),1,doc,false);
    }
    public DraftSnapshot? Get(Guid scriptId,bool includeArchived=false)
    {
        using var c=Open();using var cmd=c.CreateCommand();
        cmd.CommandText="SELECT revision,snapshot_json,archived_utc FROM editor_drafts WHERE script_id=$id;";
        cmd.Parameters.AddWithValue("$id",Id(scriptId));using var rows=cmd.ExecuteReader();
        if(!rows.Read())return null;
        bool archived=!rows.IsDBNull(2);
        if(archived&&!includeArchived)return null;
        using var doc=JsonDocument.Parse(rows.GetString(1));
        return new(Id(scriptId),rows.GetInt64(0),EditorDocumentRules.Parse(doc.RootElement),archived);
    }
    public IReadOnlyList<DraftSummary> List(bool archived=false,string query="")
    {
        if(query is null||query.Length>128||query.Any(char.IsControl))
            throw new ArgumentException("Invalid search.");
        using var c=Open();using var cmd=c.CreateCommand();
        cmd.CommandText=@"SELECT d.script_id,d.revision,s.title,d.archived_utc,
                                 COALESCE(json_array_length(json_extract(d.snapshot_json,'$.steps')),0)
                          FROM editor_drafts d JOIN scripts s ON d.script_id=s.script_id
                          WHERE d.archived_utc IS "+(archived?"NOT NULL":"NULL")+
                         " ORDER BY d.updated_utc DESC,d.script_id LIMIT 300;";
        using var rows=cmd.ExecuteReader();var result=new List<DraftSummary>();
        while(rows.Read())
        {
            string title=rows.GetString(2);
            if(query.Length>0&&!title.Contains(query,StringComparison.OrdinalIgnoreCase))continue;
            result.Add(new(rows.GetString(0),title,rows.GetInt64(1),rows.GetInt32(4),!rows.IsDBNull(3)));
        }
        return result;
    }
    public DraftSnapshot Save(Guid scriptId,long expectedRevision,EditorDocument input)
    {
        var doc=EditorDocumentRules.Validate(input);
        if(expectedRevision<=0)throw new DraftConflictException();
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        using(var update=c.CreateCommand())
        {
            update.Transaction=tx;
            update.CommandText=@"UPDATE editor_drafts SET snapshot_json=$json,revision=revision+1,updated_utc=$now
                                 WHERE script_id=$id AND revision=$rev AND archived_utc IS NULL;";
            update.Parameters.AddWithValue("$json",JsonSerializer.Serialize(doc,EditorDocumentRules.WireOptions));
            update.Parameters.AddWithValue("$now",Time());update.Parameters.AddWithValue("$id",Id(scriptId));
            update.Parameters.AddWithValue("$rev",expectedRevision);
            if(update.ExecuteNonQuery()!=1)throw new DraftConflictException();
        }
        using(var title=c.CreateCommand())
        {
            title.Transaction=tx;
            title.CommandText="UPDATE scripts SET title=$title,updated_utc=$now WHERE script_id=$id;";
            title.Parameters.AddWithValue("$title",doc.Title);title.Parameters.AddWithValue("$now",Time());
            title.Parameters.AddWithValue("$id",Id(scriptId));title.ExecuteNonQuery();
        }
        tx.Commit();return Get(scriptId)!;
    }
    public DraftSnapshot Duplicate(Guid scriptId)
    {
        DraftSnapshot original=Get(scriptId)??throw new KeyNotFoundException("Draft unavailable.");
        // Re-key every step to avoid carrying step identity from source draft into a new run.
        var target=Create(original.Document.Title+" 副本");
        return Save(Guid.Parse(target.ScriptId),target.Revision,
            original.Document with { Title=target.Document.Title,
                Steps=original.Document.Steps.Select(x=>x with {StepKey=Guid.NewGuid().ToString("D")}).ToList()});
    }
    public void Archive(Guid scriptId,long expectedRevision,string confirmation)
    {
        if(expectedRevision<=0||confirmation!="归档剧本")
            throw new ArgumentException("Explicit archive confirmation required.");
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        using(var active=c.CreateCommand())
        {
            active.Transaction=tx;
            active.CommandText=@"SELECT (SELECT COUNT(*) FROM runs WHERE script_id=$id AND state IN ('ready','running','paused','review'))+
                                  (SELECT COUNT(*) FROM group_bindings WHERE script_id=$id);";
            active.Parameters.AddWithValue("$id",Id(scriptId));
            if(Convert.ToInt64(active.ExecuteScalar())!=0)throw new InvalidOperationException("Script is referenced by a run or group.");
        }
        using var cmd=c.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText=@"UPDATE editor_drafts SET archived_utc=$now,updated_utc=$now,revision=revision+1
                           WHERE script_id=$id AND revision=$rev AND archived_utc IS NULL;";
        cmd.Parameters.AddWithValue("$now",Time());cmd.Parameters.AddWithValue("$id",Id(scriptId));
        cmd.Parameters.AddWithValue("$rev",expectedRevision);
        if(cmd.ExecuteNonQuery()!=1)throw new DraftConflictException();
        tx.Commit();
    }
    public void Restore(Guid scriptId,long expectedRevision)
    {
        if(expectedRevision<=0)throw new DraftConflictException();
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        using(var count=c.CreateCommand())
        {
            count.Transaction=tx;count.CommandText="SELECT COUNT(*) FROM editor_drafts WHERE archived_utc IS NULL;";
            if(Convert.ToInt32(count.ExecuteScalar())>=200)throw new InvalidOperationException("Too many drafts.");
        }
        using var cmd=c.CreateCommand();cmd.Transaction=tx;
        cmd.CommandText=@"UPDATE editor_drafts SET archived_utc=NULL,updated_utc=$now,revision=revision+1
                          WHERE script_id=$id AND revision=$rev AND archived_utc IS NOT NULL;";
        cmd.Parameters.AddWithValue("$now",Time());cmd.Parameters.AddWithValue("$id",Id(scriptId));
        cmd.Parameters.AddWithValue("$rev",expectedRevision);
        if(cmd.ExecuteNonQuery()!=1)throw new DraftConflictException();
        tx.Commit();
    }
}
