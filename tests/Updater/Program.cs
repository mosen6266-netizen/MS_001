using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mosen.GroupScheduler.Updater;

int n = 0;
void Check(bool ok, string message) { if (!ok) throw new Exception("FAILED: " + message); n++; Console.WriteLine("PASS " + message); }
void Deny(Action action, string message) { try { action(); } catch (Exception ex) when (ex is InvalidDataException or CryptographicException or InvalidOperationException or FormatException or KeyNotFoundException) { Check(true, message); return; } throw new Exception("FAILED " + message); }
using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
byte[] publicKey = key.ExportSubjectPublicKeyInfo();
var keys = new Dictionary<string, byte[]> { ["release-v1"] = publicKey };
byte[] package = Enumerable.Repeat((byte)42, 100*1024*1024).ToArray();
string hash = Convert.ToHexString(SHA256.HashData(package));
const string version = "0.28.0";
const string filename = "MOSEN-Telegram-Scheduler-Setup-0.28.0.exe";
const string url = "https://github.com/mosen6266-netizen/MS_001/releases/download/v0.28.0/" + filename;
string JsonPayload(string urlValue = url, string product = SignedUpdateManifest.ProductId, string sha = "", long? size = null, string target = version) =>
    JsonSerializer.Serialize(new { product, version=target, minimumVersion="0.27.0", fileName=filename, downloadUrl=urlValue, size=size??package.LongLength, sha256=sha.Length==0?hash:sha });
string Sign(string payload)
{
    byte[] bytes = Encoding.UTF8.GetBytes(payload);
    return JsonSerializer.Serialize(new { schema=1, keyId="release-v1", payload=Convert.ToBase64String(bytes),
        signature=Convert.ToBase64String(key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)) });
}
var manifest = Sign(JsonPayload());
var verified = SignedUpdateManifest.Verify(manifest, keys, "0.27.0");
Check(verified.Payload.Version==version,"valid P-256-signed update accepted");
Deny(()=>SignedUpdateManifest.Verify(manifest, new Dictionary<string,byte[]>(), "0.27.0"),"missing publisher key fails closed");
Deny(()=>SignedUpdateManifest.Verify(manifest.Replace("release-v1","attacker"),keys,"0.27.0"),"unknown key ID rejected");
Deny(()=>SignedUpdateManifest.Verify(Sign(JsonPayload(urlValue:"https://evil.example/fake.exe")),keys,"0.27.0"),"signed but off-origin installer rejected");
Deny(()=>SignedUpdateManifest.Verify(Sign(JsonPayload(product:"SIGNAL")),keys,"0.27.0"),"different product rejected");
Deny(()=>SignedUpdateManifest.Verify(Sign(JsonPayload(sha:"GG"+hash[2..])),keys,"0.27.0"),"malformed digest rejected");
Deny(()=>SignedUpdateManifest.Verify(Sign(JsonPayload(size:1024)),keys,"0.27.0"),"too small an installer rejected");
Deny(()=>SignedUpdateManifest.Verify(manifest,keys,"0.28.0"),"downgrade or equal version rejected");
Deny(()=>SignedUpdateManifest.Verify(manifest,keys,"0.26.0"),"minimum supported version enforced");
Deny(()=>SignedUpdateManifest.Verify(Sign(JsonPayload(target:"0.28.0-preview")),keys,"0.27.0"),"nonrelease version rejected");
var tampered=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(manifest)!;
var changed=Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonPayload(product:"SIGNAL")));
string tamperedManifest=$"{{\"schema\":1,\"keyId\":\"release-v1\",\"payload\":\"{changed}\",\"signature\":{tampered["signature"].GetRawText()}}}";
Deny(()=>SignedUpdateManifest.Verify(tamperedManifest,keys,"0.27.0"),"payload tamper rejected before parsing");
using var badKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
Deny(()=>SignedUpdateManifest.Verify(manifest,new Dictionary<string,byte[]>{{"release-v1",badKey.ExportSubjectPublicKeyInfo()}},"0.27.0"),"substituted public key rejected");
var temp=Path.Combine(Path.GetTempPath(),"ms001-updater-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try {
    var fake = new HttpClient(new FakeHandler((request) =>
    {
        Check(request.RequestUri?.ToString()==url,"download destination is locked by signed manifest");
        return new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(package)};
    }));
    string result = await ResumablePackageDownloader.StageAsync(fake,verified,temp);
    Check(File.Exists(result)&&new FileInfo(result).Length==package.Length,"downloaded package staged");
    Check(await ResumablePackageDownloader.IsValidAsync(result,verified.Payload),"staged file digest matches manifest");
    string second=await ResumablePackageDownloader.StageAsync(new HttpClient(new FakeHandler(_=>throw new Exception("unexpected network"))),verified,temp);
    Check(second==result,"existing verified download avoids network");
    File.Delete(result);
    var part=result+".part";
    await File.WriteAllBytesAsync(part,package.AsSpan(0,2048).ToArray());
    var resume = new HttpClient(new FakeHandler(req =>
    {
        Check(req.Headers.Range?.Ranges.Single().From==2048,"HTTP Range resume starts at exact offset");
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent){
            Content=new ByteArrayContent(package.AsMemory(2048).ToArray())};
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(2048,package.Length-1,package.Length);
        return response;
    }));
    result=await ResumablePackageDownloader.StageAsync(resume,verified,temp);
    Check(await ResumablePackageDownloader.IsValidAsync(result,verified.Payload),"resumed file has whole-file SHA256");
    File.Delete(result);
    await File.WriteAllBytesAsync(part,package.AsSpan(0,10).ToArray());
    var restart = new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(package)}));
    result=await ResumablePackageDownloader.StageAsync(restart,verified,temp);
    Check(await ResumablePackageDownloader.IsValidAsync(result,verified.Payload),"server ignores range: safely restart");
    File.Delete(result);
    File.Delete(part);
    var bad = (byte[]) package.Clone();bad[^1]=0;
    var corrupt = new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bad)}));
    try { await ResumablePackageDownloader.StageAsync(corrupt,verified,temp);throw new Exception("corrupt accepted");}
    catch(CryptographicException) {Check(!File.Exists(result)&&!File.Exists(part),"corrupted update rejected and deleted");}
    var disconnected = new HttpClient(new FakeHandler(_=>throw new HttpRequestException("offline")));
    try {await ResumablePackageDownloader.StageAsync(disconnected,verified,temp);throw new Exception("offline accepted");}
    catch(HttpRequestException){Check(!File.Exists(result),"network outage retains original application (no install attempted)");}
    Check(!File.Exists(Path.Combine(temp,"app.db")),"staging never modifies private SQLite profile");
    Console.WriteLine($"SUMMARY: {n} updater assertions passed.");
}finally {Directory.Delete(temp,true);}
sealed class FakeHandler(Func<HttpRequestMessage,HttpResponseMessage> response):HttpMessageHandler {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        => Task.FromResult(response(request));
}
