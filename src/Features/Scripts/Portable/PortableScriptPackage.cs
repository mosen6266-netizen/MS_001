using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Mosen.GroupScheduler.Scripts.Editor;
using Mosen.GroupScheduler.Scripts.Media;

namespace Mosen.GroupScheduler.Scripts.Portable;

/// <summary>Portable draft-only ZIP. No account identity, session database or outbound task information.</summary>
public static class PortableScriptPackage
{
    public const string Extension=".mosentgscript";
    public const string Format="mosen.telegram.script";
    public const int Version=1;
    public const int MaxArchiveBytes=136*1024*1024;
    private const long MaxMediaBytes=128L*1024*1024;
    private const int MaxManifestBytes=600*1024;
    private sealed record Manifest(string Format,int Version,EditorDocument Document);
    public sealed record Decoded(EditorDocument Document,IReadOnlyList<(MediaReference Reference,byte[] Bytes)> Assets);

    private static string EntryName(MediaReference item)=>"media/"+item.AssetId+(item.MimeType switch
    {
        "image/png"=>".png","image/jpeg"=>".jpg","image/webp"=>".webp",
        _=>throw new InvalidDataException("Unsupported media MIME.")
    });
    private static IReadOnlyList<MediaReference> Expected(EditorDocument document)
    {
        var results=new Dictionary<string,MediaReference>(StringComparer.Ordinal);
        foreach(var m in document.Steps.Select(x=>x.Media).Where(x=>x is not null).Cast<MediaReference>())
        {
            if(results.TryGetValue(m.AssetId,out var existing))
            {
                if(existing.MimeType!=m.MimeType || existing.ByteLength!=m.ByteLength ||
                   existing.Width!=m.Width || existing.Height!=m.Height)
                    throw new InvalidDataException("Conflicting metadata for the same image hash.");
            }
            else results.Add(m.AssetId,m);
        }
        return results.Values.ToList();
    }
    public static byte[] Export(EditorDocument document,MediaAssetStore store)
    {
        EditorDocumentRules.Validate(document);
        ArgumentNullException.ThrowIfNull(store);
        var manifest=JsonSerializer.SerializeToUtf8Bytes(new Manifest(Format,Version,document),EditorDocumentRules.WireOptions);
        if(manifest.Length>MaxManifestBytes)throw new InvalidDataException("Script manifest too large.");
        using var output=new MemoryStream();
        using(var zip=new ZipArchive(output,ZipArchiveMode.Create,leaveOpen:true))
        {
            using(var stream=zip.CreateEntry("manifest.json",CompressionLevel.Optimal).Open())
                stream.Write(manifest);
            long sum=0;
            foreach(var reference in Expected(document))
            {
                var dataUrl=store.PreviewDataUrl(reference);
                int pos=dataUrl.IndexOf(',');
                if(pos<0)throw new InvalidDataException("Local media unavailable.");
                var bytes=Convert.FromBase64String(dataUrl[(pos+1)..]);
                sum+=bytes.Length;
                if(sum>MaxMediaBytes)throw new InvalidDataException("Image quota exceeded.");
                using var stream=zip.CreateEntry(EntryName(reference),CompressionLevel.Optimal).Open();
                stream.Write(bytes);
            }
        }
        if(output.Length>MaxArchiveBytes)throw new InvalidDataException("Script ZIP too large.");
        return output.ToArray();
    }
    private static byte[] ReadBounded(ZipArchiveEntry entry,int maximum)
    {
        if(entry.Length<0 || entry.Length>maximum)throw new InvalidDataException("Uncompressed entry too large.");
        using var input=entry.Open();
        using var output=new MemoryStream();
        var chunk=new byte[32*1024];
        int n;
        while((n=input.Read(chunk))>0)
        {
            if(output.Length+n>maximum)throw new InvalidDataException("ZIP expansion limit exceeded.");
            output.Write(chunk,0,n);
        }
        if(output.Length!=entry.Length)throw new InvalidDataException("ZIP length mismatch.");
        return output.ToArray();
    }
    public static Decoded Decode(byte[] bytes)
    {
        if(bytes is null || bytes.Length is < 32 or > MaxArchiveBytes)
            throw new InvalidDataException("Invalid ZIP size.");
        using var input=new MemoryStream(bytes,writable:false);
        using var zip=new ZipArchive(input,ZipArchiveMode.Read);
        if(zip.Entries.Count is < 1 or > 1001)throw new InvalidDataException("Too many ZIP entries.");
        var entries=new Dictionary<string,ZipArchiveEntry>(StringComparer.Ordinal);
        long total=0;
        foreach(var entry in zip.Entries)
        {
            if(entry.FullName.Contains('\\') || entry.FullName.StartsWith('/') ||
               entry.FullName.Contains("..",StringComparison.Ordinal) ||
               entry.FullName.EndsWith('/') || entry.Name.Length==0 ||
               !entries.TryAdd(entry.FullName,entry))
                throw new InvalidDataException("Unsafe or duplicate ZIP entry.");
            // Unix symlinks and non-regular entries are not allowed.
            int mode=(entry.ExternalAttributes>>16)&0xF000;
            if(mode!=0 && mode!=0x8000)throw new InvalidDataException("Non-regular ZIP entry.");
            total=checked(total+entry.Length);
            if(total>MaxMediaBytes+MaxManifestBytes)throw new InvalidDataException("ZIP expansion budget exceeded.");
        }
        if(!entries.TryGetValue("manifest.json",out var manifestEntry))
            throw new InvalidDataException("Missing manifest.");
        var manifestBytes=ReadBounded(manifestEntry,MaxManifestBytes);
        Manifest? manifest;
        try { manifest=JsonSerializer.Deserialize<Manifest>(manifestBytes,EditorDocumentRules.WireOptions); }
        catch(JsonException e){throw new InvalidDataException("Invalid manifest JSON.",e);}
        if(manifest is null || manifest.Format!=Format || manifest.Version!=Version || manifest.Document is null)
            throw new InvalidDataException("Unsupported script format.");
        var document=EditorDocumentRules.Validate(manifest.Document);
        var expected=Expected(document);
        if(entries.Count!=expected.Count+1)throw new InvalidDataException("Unexpected ZIP file(s).");
        var assets=new List<(MediaReference Reference,byte[] Bytes)>();
        foreach(var reference in expected)
        {
            if(!entries.TryGetValue(EntryName(reference),out var entry))
                throw new InvalidDataException("Missing script image.");
            var data=ReadBounded(entry,MediaAssetStore.MaxImageBytes);
            if(data.LongLength!=reference.ByteLength ||
               Convert.ToHexStringLower(SHA256.HashData(data))!=reference.AssetId)
                throw new InvalidDataException("Portable image checksum mismatch.");
            assets.Add((reference,data));
        }
        return new(document,assets);
    }
    public static EditorDocument InstallMedia(Decoded decoded,MediaAssetStore store)
    {
        ArgumentNullException.ThrowIfNull(decoded);
        ArgumentNullException.ThrowIfNull(store);
        // Decode validates the whole archive before any local media writes.
        foreach(var (reference,bytes) in decoded.Assets)
        {
            var written=store.ImportBase64(reference.FileName,reference.MimeType,Convert.ToBase64String(bytes));
            if(written.AssetId!=reference.AssetId || written.ByteLength!=reference.ByteLength ||
               written.Width!=reference.Width || written.Height!=reference.Height)
                throw new InvalidDataException("Portable image metadata mismatch.");
        }
        return decoded.Document;
    }
}