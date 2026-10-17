using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mosen.GroupScheduler.Updater;

// Restricted release-runner CLI. PKCS8 only from secret environment variable, never CLI/history/logs.
// Usage: dotnet run --project updater/ReleaseSigner -- <version> <signed-installer-exe> <manifest-out> [minimumVersion]
if(args.Length is < 3 or > 4)throw new ArgumentException("Usage: <version> <signed installer> <manifest destination> [minimum version]");
string version=args[0];
string path=Path.GetFullPath(args[1]);
string output=Path.GetFullPath(args[2]);
string minimum=args.Length==4?args[3]:"0.27.0";
SignedUpdateManifest.ParseVersion(version);
SignedUpdateManifest.ParseVersion(minimum);
if(SignedUpdateManifest.ParseVersion(minimum)>SignedUpdateManifest.ParseVersion(version))
    throw new InvalidDataException("Minimum version higher than target");
string file=$"MOSEN-Telegram-Scheduler-Setup-{version}.exe";
if(Path.GetFileName(path)!=file)throw new InvalidDataException("Noncanonical release file name");
var info=new FileInfo(path);
if(!info.Exists || info.Length<100L*1024*1024 || info.Length>768L*1024*1024)
    throw new InvalidDataException("Invalid offline package size");
string pin=File.ReadAllText("updater/trust/release-v1.spki.base64").Trim();
if(pin=="UNCONFIGURED" || string.IsNullOrWhiteSpace(pin))throw new CryptographicException("Publisher key is not enrolled");
string secret=Environment.GetEnvironmentVariable("UPDATE_SIGNING_PKCS8_BASE64")??"";
if(string.IsNullOrWhiteSpace(secret))throw new CryptographicException("Missing release-only signing secret");
byte[] publicSpki=Convert.FromBase64String(pin);
byte[] privatePkcs8=Convert.FromBase64String(secret);
using var key=ECDsa.Create();
try {
    key.ImportPkcs8PrivateKey(privatePkcs8,out int imported);
    if(imported!=privatePkcs8.Length || key.KeySize!=256 ||
        !CryptographicOperations.FixedTimeEquals(key.ExportSubjectPublicKeyInfo(),publicSpki))
        throw new CryptographicException("Signing key does not match pinned public trust anchor");
    await using var stream=File.OpenRead(path);
    var sha=Convert.ToHexString(await SHA256.HashDataAsync(stream));
    var payload=JsonSerializer.Serialize(new {
        product=SignedUpdateManifest.ProductId,version,minimumVersion=minimum,fileName=file,
        downloadUrl=$"https://github.com/mosen6266-netizen/MS_001/releases/download/v{version}/{file}",
        size=info.Length,sha256=sha
    });
    byte[] payloadBytes=Encoding.UTF8.GetBytes(payload);
    var signature=key.SignData(payloadBytes,HashAlgorithmName.SHA256,
        DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    string envelope=JsonSerializer.Serialize(new {
        schema=1,keyId="release-v1",
        payload=Convert.ToBase64String(payloadBytes),
        signature=Convert.ToBase64String(signature)
    });
    // Verify with the actual client contract before any upload.
    SignedUpdateManifest.Verify(envelope,new Dictionary<string,byte[]> { ["release-v1"]=publicSpki },minimum);
    await File.WriteAllTextAsync(output,envelope,new UTF8Encoding(false));
    Console.WriteLine($"Created signed update manifest for v{version}, SHA256={sha}; no key material logged.");
}finally {
    CryptographicOperations.ZeroMemory(privatePkcs8);
}
