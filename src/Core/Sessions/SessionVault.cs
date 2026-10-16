using System.Security.Cryptography;
using System.Text;
using Mosen.GroupScheduler.Core.Security;

namespace Mosen.GroupScheduler.Core.Sessions;

/// <summary>
/// Per-account encrypted session-key vault. The secret never enters app.db or logs.
/// Keys require Windows CurrentUser DPAPI in production; test doubles remain isolated.
/// </summary>
public interface IKeyEnvelopeProtector
{
    string Protect(ReadOnlySpan<byte> key);
    byte[] Unprotect(string envelope);
}
public sealed class WindowsUserKeyProtector : IKeyEnvelopeProtector
{
    public string Protect(ReadOnlySpan<byte> key)=>DpapiProtector.Protect(key);
    public byte[] Unprotect(string envelope)=>DpapiProtector.Unprotect(envelope);
}
public sealed class SessionKeyLease : IDisposable
{
    private byte[]? _key;
    internal SessionKeyLease(byte[] key) => _key=key;
    public ReadOnlySpan<byte> Bytes=>_key??throw new ObjectDisposedException(nameof(SessionKeyLease));
    public void Dispose()
    {
        byte[]? prior=Interlocked.Exchange(ref _key,null);
        if(prior is not null)CryptographicOperations.ZeroMemory(prior);
    }
}
public sealed record SessionPaths(string DatabaseDirectory,string FilesDirectory);
public sealed class SessionVault
{
    private readonly string _root;
    private readonly IKeyEnvelopeProtector _protector;
    private readonly object _gate=new();
    public SessionVault(string root,IKeyEnvelopeProtector protector)
    {
        if(string.IsNullOrWhiteSpace(root))throw new ArgumentException("Missing session root.",nameof(root));
        _root=Path.GetFullPath(root);
        _protector=protector??throw new ArgumentNullException(nameof(protector));
    }
    public static SessionVault ForCurrentUser()
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Windows DPAPI is mandatory for real session storage.");
        var root=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if(string.IsNullOrEmpty(root))throw new InvalidOperationException("LocalAppData unavailable.");
        return new SessionVault(Path.Combine(root,"MOSEN","TelegramScheduler","tdlib-sessions"),new WindowsUserKeyProtector());
    }
    private static void Validate(Guid id)
    {
        if(id==Guid.Empty)throw new ArgumentException("Account ID cannot be empty.");
    }
    private string AccountFolder(Guid id)=>Path.Combine(_root,id.ToString("D"));
    private static void CheckNotReparse(string path)
    {
        if(File.Exists(path)||Directory.Exists(path))
        {
            if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)
                throw new InvalidOperationException("Symlink/reparse directory is not allowed for session secrets.");
        }
    }
    private string EnvelopeFile(Guid id)=>Path.Combine(AccountFolder(id),"db-key.dpapi");
    public bool Exists(Guid id)
    {
        Validate(id);
        lock(_gate){return File.Exists(EnvelopeFile(id));}
    }
    public SessionPaths Paths(Guid id)
    {
        Validate(id);
        string slot=AccountFolder(id);
        return new(Path.Combine(slot,"database"),Path.Combine(slot,"files"));
    }
    /// <summary>Atomic first-time creation; refuses overwrite to preserve sessions across upgrades.</summary>
    public void InitializeNew(Guid id)
    {
        Validate(id);
        lock(_gate)
        {
            Directory.CreateDirectory(_root);
            CheckNotReparse(_root);
            string slot=AccountFolder(id);
            Directory.CreateDirectory(slot);
            CheckNotReparse(slot);
            string envelopePath=EnvelopeFile(id);
            CheckNotReparse(envelopePath);
            if(File.Exists(envelopePath))return; // do not rotate/re-encrypt existing session key
            byte[] key=RandomNumberGenerator.GetBytes(32);
            string temp=Path.Combine(slot,".key-"+Guid.NewGuid().ToString("N")+".tmp");
            try
            {
                string envelope=_protector.Protect(key);
                File.WriteAllText(temp,envelope,Encoding.ASCII);
                File.Move(temp,envelopePath,overwrite:false); // atomic same-volume install; no accidental rewrite
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
                if(File.Exists(temp))File.Delete(temp);
            }
        }
    }
    public SessionKeyLease OpenKey(Guid id)
    {
        Validate(id);
        lock(_gate)
        {
            string path=EnvelopeFile(id);
            CheckNotReparse(_root);
            CheckNotReparse(AccountFolder(id));
            CheckNotReparse(path);
            if(!File.Exists(path))throw new FileNotFoundException("Protected session key does not exist.");
            if(new FileInfo(path).Length is < 20 or > 131072)throw new InvalidDataException("Invalid protected key length.");
            byte[] value=_protector.Unprotect(File.ReadAllText(path,Encoding.ASCII));
            if(value.Length==32)return new SessionKeyLease(value);
            CryptographicOperations.ZeroMemory(value);
            throw new CryptographicException("Invalid decrypted database key length.");
        }
    }
    public void EnsureDirectories(Guid id)
    {
        Validate(id);
        lock(_gate)
        {
            var paths=Paths(id);
            CheckNotReparse(_root);CheckNotReparse(AccountFolder(id));
            if(!Exists(id))throw new FileNotFoundException("Session key must exist before opening TDLib database.");
            Directory.CreateDirectory(paths.DatabaseDirectory);
            Directory.CreateDirectory(paths.FilesDirectory);
            CheckNotReparse(paths.DatabaseDirectory);CheckNotReparse(paths.FilesDirectory);
        }
    }
    // Intentional: no deletion method. Archived accounts retain protected keys and TDLib databases
    // until a separate, audited explicit removal workflow is implemented.
}
