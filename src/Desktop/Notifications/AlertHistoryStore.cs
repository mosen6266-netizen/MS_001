using System.Text.Json;

namespace Mosen.GroupScheduler.Notifications;
/// <summary>Persist only constrained operational IDs/categories; no raw exception or chat content.</summary>
public sealed class AlertHistoryStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions Options=new(JsonSerializerDefaults.Web);
    public AlertHistoryStore(string filePath)
    {
        if(string.IsNullOrWhiteSpace(filePath))throw new ArgumentException("History path missing.");
        _path=Path.GetFullPath(filePath);
    }
    public IReadOnlyList<OperationalAlert> Load()
    {
        if(!File.Exists(_path))return Array.Empty<OperationalAlert>();
        try
        {
            var info=new FileInfo(_path);
            if(info.Length>200_000)throw new InvalidDataException("Oversized history.");
            var records=JsonSerializer.Deserialize<List<OperationalAlert>>(File.ReadAllText(_path),Options)
                ??throw new InvalidDataException("Unreadable history.");
            if(records.Count>500||records.Any(x=>x is null||
                !Enum.IsDefined(x.Category)||x.Key!=(x.Category+":"+x.Key.Split(':').Last())||
                x.Key.Length>120||x.Occurrences<1||x.FirstUtc>x.LatestUtc))
                throw new InvalidDataException("Invalid stored alerts.");
            return records;
        }
        catch(Exception e) when(e is JsonException or IOException or InvalidDataException or UnauthorizedAccessException)
        {return Array.Empty<OperationalAlert>();}
    }
    public bool Save(IReadOnlyList<OperationalAlert> history)
    {
        if(history.Count>500)return false;
        var temporary=_path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(temporary,JsonSerializer.Serialize(history,Options));
            File.Move(temporary,_path,overwrite:true);
            return true;
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException)
        {return false;}
        finally {try{if(File.Exists(temporary))File.Delete(temporary);}catch(IOException){}}
    }
}
