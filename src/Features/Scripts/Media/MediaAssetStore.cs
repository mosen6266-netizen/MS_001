using System.Buffers.Binary;
using System.Security.Cryptography;
using Mosen.GroupScheduler.Scripts.Editor;

namespace Mosen.GroupScheduler.Scripts.Media;

/// <summary>Local content-addressed assets. Never follows paths supplied by WebView2.</summary>
public sealed class MediaAssetStore
{
    public const int MaxImageBytes=5*1024*1024;
    private const long TotalQuotaBytes=128L*1024*1024;
    private readonly string _root;
    public string RootPath => _root;
    private readonly object _gate=new();
    public MediaAssetStore(string root)
    {
        if(string.IsNullOrWhiteSpace(root))throw new ArgumentException("Invalid local media store.");
        _root=Path.GetFullPath(root);
    }
    public static MediaAssetStore ForCurrentUser()
    {
        var local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if(string.IsNullOrWhiteSpace(local))throw new InvalidOperationException("LocalAppData unavailable.");
        return new MediaAssetStore(Path.Combine(local,"MOSEN","TelegramScheduler","media-assets"));
    }
    private static string Extension(string mime)=>mime switch
    {
        "image/png"=>".png","image/jpeg"=>".jpg","image/webp"=>".webp",
        _=>throw new ArgumentException("Only PNG, JPEG and WebP images are allowed.")
    };
    private void EnsureSafeDirectory()
    {
        Directory.CreateDirectory(_root);
        if((File.GetAttributes(_root)&FileAttributes.ReparsePoint)!=0)
            throw new IOException("Media root must not be a reparse point.");
    }
    private static string SafeName(string raw)
    {
        if(string.IsNullOrEmpty(raw))return "image";
        var label=new string(raw.Take(128).Select(c=>char.IsControl(c)||c is '/' or '\\' or ':' ? '_' : c).ToArray()).Trim();
        return label.Length==0?"image":label;
    }
    private string AssetPath(string hash,string mime)
    {
        if(hash is null||hash.Length!=64||hash.Any(c=>c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new ArgumentException("Invalid image content hash.");
        return Path.Combine(_root,hash+Extension(mime));
    }
    public MediaReference ImportBase64(string name,string claimedMime,string base64)
    {
        Extension(claimedMime);
        if(base64 is null||base64.Length<40||base64.Length>((MaxImageBytes+2)/3)*4+8)
            throw new ArgumentException("Image exceeds the size limit.");
        byte[] bytes=Convert.FromBase64String(base64);
        if(bytes.Length is <32 or >MaxImageBytes)throw new ArgumentException("Image must be 32 bytes to 5 MiB.");
        var (actualMime,w,h)=Inspect(bytes);
        if(claimedMime!=actualMime)throw new ArgumentException("Image content does not match the claimed type.");
        string hash=Convert.ToHexStringLower(SHA256.HashData(bytes));
        string path=AssetPath(hash,actualMime);
        lock(_gate)
        {
            EnsureSafeDirectory();
            if(File.Exists(path))
            {
                if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)
                    throw new IOException("Unsafe media file.");
                byte[] existing=File.ReadAllBytes(path);
                if(!SHA256.HashData(existing).AsSpan().SequenceEqual(SHA256.HashData(bytes)))
                    throw new IOException("Stored media hash collision or corruption.");
            }
            else
            {
                long used=Directory.EnumerateFiles(_root,"*",SearchOption.TopDirectoryOnly)
                    .Sum(f=>new FileInfo(f).Length);
                if(used+bytes.Length>TotalQuotaBytes)throw new IOException("Media storage quota exceeded.");
                string temp=Path.Combine(_root,".partial-"+Guid.NewGuid().ToString("N"));
                try
                {
                    using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                    {stream.Write(bytes);stream.Flush(flushToDisk:true);}
                    File.Move(temp,path,overwrite:false);
                }
                finally {if(File.Exists(temp))File.Delete(temp);}
            }
        }
        return new(hash,actualMime,bytes.LongLength,w,h,SafeName(name));
    }
    public string PreviewDataUrl(MediaReference reference)
    {
        if(reference is null)throw new ArgumentNullException(nameof(reference));
        string path=AssetPath(reference.AssetId,reference.MimeType);
        lock(_gate)
        {
            EnsureSafeDirectory();
            if(!File.Exists(path)||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)
                throw new FileNotFoundException("Image was not found in the local media store.");
            var data=File.ReadAllBytes(path);
            if(data.LongLength!=reference.ByteLength||data.Length>MaxImageBytes||
               Convert.ToHexStringLower(SHA256.HashData(data))!=reference.AssetId)
                throw new IOException("Local image checksum is invalid.");
            var (mime,w,h)=Inspect(data);
            if(mime!=reference.MimeType||w!=reference.Width||h!=reference.Height)
                throw new IOException("Local image metadata mismatch.");
            return "data:"+mime+";base64,"+Convert.ToBase64String(data);
        }
    }
    private static (string Mime,int Width,int Height) Inspect(byte[] bytes)
    {
        var b=bytes.AsSpan();
        if(b.Length>=57&&b[..8].SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))
        {
            if(BinaryPrimitives.ReadUInt32BigEndian(b.Slice(8,4))!=13 ||
               !b.Slice(12,4).SequenceEqual("IHDR"u8))throw new ArgumentException("PNG missing IHDR.");
            int width=checked((int)BinaryPrimitives.ReadUInt32BigEndian(b.Slice(16,4)));
            int height=checked((int)BinaryPrimitives.ReadUInt32BigEndian(b.Slice(20,4)));
            if(!b.Slice(b.Length-12,4).SequenceEqual(new byte[]{0,0,0,0}) ||
               !b.Slice(b.Length-8,4).SequenceEqual("IEND"u8))
                throw new ArgumentException("PNG truncated or malformed.");
            return ValidateDims("image/png",width,height);
        }
        if(b.Length>=32&&b[0]==0xff&&b[1]==0xd8&&b[^2]==0xff&&b[^1]==0xd9)
        {
            int offset=2;
            while(offset+9<b.Length)
            {
                if(b[offset++]!=0xff)break;
                while(offset<b.Length&&b[offset]==0xff)offset++;
                if(offset>=b.Length)break;
                byte marker=b[offset++];
                if(marker is 0xd8 or 0xd9 or 0x01 || marker is >=0xd0 and <=0xd7)continue;
                if(offset+2>b.Length)break;
                int segment=BinaryPrimitives.ReadUInt16BigEndian(b.Slice(offset,2));
                if(segment<2||offset+segment>b.Length)break;
                if(marker is 0xc0 or 0xc1 or 0xc2 or 0xc3 or 0xc5 or 0xc6 or 0xc7 or 0xc9 or 0xca or 0xcb or 0xcd or 0xce or 0xcf)
                {
                    if(segment<7)break;
                    int height=BinaryPrimitives.ReadUInt16BigEndian(b.Slice(offset+3,2));
                    int width=BinaryPrimitives.ReadUInt16BigEndian(b.Slice(offset+5,2));
                    return ValidateDims("image/jpeg",width,height);
                }
                offset+=segment;
            }
            throw new ArgumentException("JPEG image dimensions missing.");
        }
        if(b.Length>=30&&b[..4].SequenceEqual("RIFF"u8)&&b.Slice(8,4).SequenceEqual("WEBP"u8)&&
           BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(4,4))==b.Length-8)
        {
            var fmt=b.Slice(12,4);
            if(fmt.SequenceEqual("VP8X"u8)&&b.Length>=30)
            {
                int w=1+b[24]+(b[25]<<8)+(b[26]<<16);
                int h=1+b[27]+(b[28]<<8)+(b[29]<<16);
                return ValidateDims("image/webp",w,h);
            }
            if(fmt.SequenceEqual("VP8L"u8)&&b.Length>=25&&b[20]==0x2f)
            {
                int w=1+(((b[22]&0x3f)<<8)|b[21]);
                int h=1+((b[24]&0x0f)<<10)|(b[23]<<2)|((b[22]&0xc0)>>6);
                return ValidateDims("image/webp",w,h);
            }
        }
        throw new ArgumentException("Not a valid supported image signature.");
    }
    private static (string,int,int) ValidateDims(string mime,int width,int height)
    {
        if(width is <1 or >8192||height is <1 or >8192)
            throw new ArgumentException("Image dimensions out of range.");
        return (mime,width,height);
    }
}
