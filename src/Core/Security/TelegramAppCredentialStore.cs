using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Mosen.GroupScheduler.Core.Security;

/// <summary>
/// A user's Telegram API application credentials; protected for this Windows account.
/// This is NOT an account session, sign-in permission, or background sender.
/// Never return the hash to the WebView, telemetry, logs or exported diagnostics.
/// </summary>
public sealed class TelegramAppCredentialStore
{
    private readonly string _file;
    public TelegramAppCredentialStore(string filePath)
    {
        if(string.IsNullOrWhiteSpace(filePath)||!Path.IsPathFullyQualified(filePath))
            throw new ArgumentException("Credential storage path must be absolute.");
        _file=Path.GetFullPath(filePath);
    }

    public bool Configured
    {
        get {
            if(!File.Exists(_file))return false;
            try{
                using var document=JsonDocument.Parse(File.ReadAllText(_file));
                var root=document.RootElement;
                return root.GetProperty("schemaVersion").GetInt32()==1 &&
                    root.GetProperty("apiId").GetInt32()>0 &&
                    root.GetProperty("protectedApiHash").GetString()?.StartsWith("MS001-DPAPI-USER-v1:",StringComparison.Ordinal)==true;
            }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
            {return false;}
        }
    }

    public void Save(int apiId,string apiHash)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        if(apiId<1 || apiHash is null || apiHash.Length!=32 || !apiHash.All(Uri.IsHexDigit))
            throw new ArgumentException("Invalid Telegram API application credentials.");
        string dir=Path.GetDirectoryName(_file)!;
        Directory.CreateDirectory(dir);
        byte[] clear=Encoding.ASCII.GetBytes(apiHash.ToLowerInvariant());
        string encrypted;
        try{encrypted=DpapiProtector.Protect(clear);}
        finally{CryptographicOperations.ZeroMemory(clear);}
        var data=JsonSerializer.Serialize(new {schemaVersion=1,apiId,protectedApiHash=encrypted});
        var tmp=Path.Combine(dir,"."+Path.GetFileName(_file)+"."+Guid.NewGuid().ToString("N")+".tmp");
        try{
            File.WriteAllText(tmp,data,new UTF8Encoding(false));
            File.Move(tmp,_file,overwrite:true);
        }finally{if(File.Exists(tmp))File.Delete(tmp);}
    }
    public (int ApiId,string ApiHash) Read()
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        using var document=JsonDocument.Parse(File.ReadAllText(_file));
        var root=document.RootElement;
        if(root.GetProperty("schemaVersion").GetInt32()!=1)throw new InvalidDataException("Unknown credential format.");
        int id=root.GetProperty("apiId").GetInt32();
        byte[] decoded=DpapiProtector.Unprotect(root.GetProperty("protectedApiHash").GetString()!);
        try{
            string hash=Encoding.ASCII.GetString(decoded);
            if(id<1||hash.Length!=32||!hash.All(Uri.IsHexDigit))
                throw new InvalidDataException("Stored application credentials are invalid.");
            return(id,hash);
        }finally{CryptographicOperations.ZeroMemory(decoded);}
    }
    public void Clear()
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        if(File.Exists(_file))File.Delete(_file);
    }
}
