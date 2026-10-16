using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Mosen.GroupScheduler.Persistence;
using Mosen.GroupScheduler.Scripts.Editor;
using Mosen.GroupScheduler.Scripts.Media;

namespace Mosen.GroupScheduler.Scripts.Preview;

public sealed record PublishedScript(string ScriptId,int Version,long SourceRevision,
    string Sha256,DateTimeOffset CreatedUtc,EditorDocument Document);
/// <summary>Only a user-published immutable version is eligible for a future run.</summary>
public sealed class PublishedScriptRepository
{
    private readonly AppDatabase _database;
    private readonly MediaAssetStore _media;
    public PublishedScriptRepository(AppDatabase database,MediaAssetStore media)
    { _database=database??throw new ArgumentNullException(nameof(database));
      _media=media??throw new ArgumentNullException(nameof(media)); }
    private SqliteConnection Open()
    {
        _database.Initialize();
        var c=new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource=_database.FilePath,Mode=SqliteOpenMode.ReadWrite,
            ForeignKeys=true,Pooling=false,Cache=SqliteCacheMode.Private,DefaultTimeout=10
        }.ToString());
        c.Open();
        using var pragma=c.CreateCommand();
        pragma.CommandText="PRAGMA foreign_keys=ON;PRAGMA busy_timeout=10000;";
        pragma.ExecuteNonQuery();
        return c;
    }
    private static string Stamp()=>DateTimeOffset.UtcNow.ToString("O");
    private static string Hash(string json)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    private static EditorDocument Parse(string json)
    { using var doc=JsonDocument.Parse(json); return EditorDocumentRules.Parse(doc.RootElement); }
    public PublishedScript Publish(Guid scriptId,long expectedRevision)
    {
        if(scriptId==Guid.Empty||expectedRevision<1)throw new ArgumentException("Invalid publish request.");
        using var c=Open();using var tx=c.BeginTransaction(deferred:false);
        string json;
        using(var query=c.CreateCommand())
        {
            query.Transaction=tx;
            query.CommandText=@"SELECT snapshot_json FROM editor_drafts
                WHERE script_id=$id AND archived_utc IS NULL AND revision=$rev;";
            query.Parameters.AddWithValue("$id",scriptId.ToString("D"));
            query.Parameters.AddWithValue("$rev",expectedRevision);
            json=query.ExecuteScalar() as string ?? throw new DraftConflictException();
        }
        var doc=Parse(json);
        if(doc.Steps.Count==0)throw new InvalidOperationException("Cannot publish an empty script.");
        // Validate all assets before introducing any referenced published version.
        var media=doc.Steps.Where(x=>x.Media is not null).Select(x=>x.Media!)
            .GroupBy(x=>x.AssetId,StringComparer.Ordinal)
            .Select(x=>x.First()).ToArray();
        foreach(var item in media) _= _media.PreviewDataUrl(item);
        int version;
        using(var next=c.CreateCommand())
        {
            next.Transaction=tx;
            next.CommandText="SELECT COALESCE(MAX(version),0)+1 FROM script_versions WHERE script_id=$id;";
            next.Parameters.AddWithValue("$id",scriptId.ToString("D"));
            version=Convert.ToInt32(next.ExecuteScalar());
        }
        var sha=Hash(json);var now=Stamp();
        void Exec(string sql,params (string Name,object Value)[] args)
        {
            using var cmd=c.CreateCommand();cmd.Transaction=tx;cmd.CommandText=sql;
            foreach(var (key,value) in args)cmd.Parameters.AddWithValue(key,value);
            cmd.ExecuteNonQuery();
        }
        var id=scriptId.ToString("D");
        Exec("INSERT INTO script_versions(script_id,version,published,created_utc) VALUES($id,$v,1,$now);",
            ("$id",id),("$v",version),("$now",now));
        Exec(@"INSERT INTO published_script_snapshots(script_id,version,source_revision,snapshot_json,sha256,created_utc)
               VALUES($id,$v,$rev,$doc,$sha,$now);",
            ("$id",id),("$v",version),("$rev",expectedRevision),("$doc",json),("$sha",sha),("$now",now));
        foreach(var role in doc.Roles)
            Exec(@"INSERT INTO script_roles(script_id,script_version,role_key,role_label)
                   VALUES($id,$v,$key,$label);",
                ("$id",id),("$v",version),("$key",role.RoleKey),("$label",role.RoleLabel));
        foreach(var item in media)
        {
            var ext=item.MimeType switch {"image/png"=>".png","image/jpeg"=>".jpg","image/webp"=>".webp",_=>throw new ArgumentException("Unsupported image.")};
            Exec(@"INSERT OR IGNORE INTO media_assets(sha256,relative_path,media_type,byte_size,created_utc)
                   VALUES($sha,$path,$mime,$bytes,$now);",
                ("$sha",item.AssetId),("$path",item.AssetId+ext),("$mime",item.MimeType),
                ("$bytes",item.ByteLength),("$now",now));
        }
        for(int index=0;index<doc.Steps.Count;index++)
        {
            var step=doc.Steps[index];
            Exec(@"INSERT INTO script_steps(script_id,script_version,step_key,ordinal,role_key,body,
                  pre_send_delay_ms,typing_duration_ms,post_send_delay_ms,media_sha256)
                  VALUES($id,$v,$key,$ord,$role,$body,$pre,$typing,$post,$media);",
                ("$id",id),("$v",version),("$key",step.StepKey),("$ord",index),
                ("$role",step.RoleKey),("$body",step.Body),("$pre",step.PreSendDelayMs),
                ("$typing",step.TypingDurationMs),("$post",step.PostSendDelayMs),
                ("$media",step.Media?.AssetId??(object)DBNull.Value));
        }
        tx.Commit();
        return new(id,version,expectedRevision,sha,DateTimeOffset.Parse(now),Parse(json));
    }
    public PublishedScript? Get(Guid scriptId,int version)
    {
        if(scriptId==Guid.Empty || version<1)throw new ArgumentException("Invalid published version.");
        using var c=Open();using var cmd=c.CreateCommand();
        cmd.CommandText=@"SELECT source_revision,snapshot_json,sha256,created_utc FROM published_script_snapshots
                          WHERE script_id=$id AND version=$v;";
        cmd.Parameters.AddWithValue("$id",scriptId.ToString("D"));cmd.Parameters.AddWithValue("$v",version);
        using var row=cmd.ExecuteReader();
        if(!row.Read())return null;
        var revision=row.GetInt64(0);var json=row.GetString(1);var checksum=row.GetString(2);
        if(!string.Equals(Hash(json),checksum,StringComparison.Ordinal))
            throw new InvalidDataException("Published script checksum failed.");
        return new(scriptId.ToString("D"),version,revision,checksum,
            DateTimeOffset.Parse(row.GetString(3)),Parse(json));
    }
    public IReadOnlyList<(int Version,long SourceRevision,string Sha256)> List(Guid scriptId)
    {
        if(scriptId==Guid.Empty)throw new ArgumentException("Invalid script ID.");
        using var c=Open();using var cmd=c.CreateCommand();
        cmd.CommandText=@"SELECT version,source_revision,sha256 FROM published_script_snapshots
                           WHERE script_id=$id ORDER BY version DESC LIMIT 100;";
        cmd.Parameters.AddWithValue("$id",scriptId.ToString("D"));
        using var rows=cmd.ExecuteReader();
        var found=new List<(int,long,string)>();
        while(rows.Read())found.Add((rows.GetInt32(0),rows.GetInt64(1),rows.GetString(2)));
        return found;
    }
}
