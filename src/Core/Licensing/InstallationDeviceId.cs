using System.Security.Cryptography;
using System.Text;

namespace Mosen.GroupScheduler.Licensing;
/// <summary>Per-installation random pseudonym, not a machine serial, MAC, phone or login.
/// Future Windows packaging can store this random seed with user-specific DPAPI.</summary>
public static class InstallationDeviceId
{
    public static string LoadOrCreate(string filePath)
    {
        if(string.IsNullOrWhiteSpace(filePath))throw new ArgumentException("Device profile path required.");
        var path=Path.GetFullPath(filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if(!File.Exists(path))
        {
            try
            {
                using var fs=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
                var data=RandomNumberGenerator.GetBytes(32);
                fs.Write(data);fs.Flush(flushToDisk:true);
                CryptographicOperations.ZeroMemory(data);
            }
            catch(IOException) when(File.Exists(path)){ /* other process created it */ }
        }
        var buffer=File.ReadAllBytes(path);
        if(buffer.Length!=32)throw new InvalidDataException("Installation identity is invalid; refuse new binding.");
        try
        {
            var scoped=Encoding.UTF8.GetBytes(SignedLicenseVerifier.ProductId+":");
            var hashed=SHA256.HashData(scoped.Concat(buffer).ToArray());
            return Convert.ToHexString(hashed).ToLowerInvariant();
        }
        finally{CryptographicOperations.ZeroMemory(buffer);}
    }
}
