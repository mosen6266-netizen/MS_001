using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mosen.GroupScheduler.Licensing;

int count=0;
void Check(bool yes,string label){if(!yes)throw new Exception("FAIL: "+label);count++;Console.WriteLine("PASS "+label);}
void Reject<T>(Action act,string label)where T:Exception
{bool caught=false;try{act();}catch(T){caught=true;}Check(caught,label);}
var now=DateTimeOffset.UtcNow;
using var signing=ECDsa.Create(ECCurve.NamedCurves.nistP256);
using var otherSigner=ECDsa.Create(ECCurve.NamedCurves.nistP256);
var publicKey=Convert.ToBase64String(signing.ExportSubjectPublicKeyInfo());
var device=Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
var nonce=Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
var id=Guid.NewGuid().ToString("D");
var baseClaims=new LicenseClaims(SignedLicenseVerifier.ProductId,id,device,
 now.AddDays(30).ToUnixTimeMilliseconds(),now.ToUnixTimeMilliseconds(),
 now.AddMinutes(60).ToUnixTimeMilliseconds(),1,"valid",nonce);
LicenseEnvelope Signed(LicenseClaims c,ECDsa? key=null,bool ok=true)
{
 var sig=(key??signing).SignData(Encoding.UTF8.GetBytes(SignedLicenseVerifier.Canonical(c)),
    HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
 return new LicenseEnvelope(ok,c,Convert.ToBase64String(sig).TrimEnd('=').Replace('+','-').Replace('/','_'));
}
var gate=new SignedLicenseVerifier(publicKey,device);
Check(!gate.Offline(now).Authorized,"unactivated client is disabled by default");
Check(gate.Apply(null,nonce,now).Code=="NO_SIGNED_LEASE","missing signed lease rejected");
Check(gate.Apply(Signed(baseClaims,ok:false),nonce,now).Authorized==false,"explicit denied response cannot authorize");
var accepted=gate.Apply(Signed(baseClaims),nonce,now);
Check(accepted.Authorized&&accepted.Code=="VALID","genuine P-256 signed Telegram license accepted");
Check(accepted.LeaseExpiresUtc?.ToUnixTimeMilliseconds()==baseClaims.LeaseUntilMs,"offline window strictly capped at one hour");
Check(gate.Offline(now.AddMinutes(30)).Authorized,"verified lease supports bounded in-process offline interval");
Check(!gate.Offline(now.AddHours(2)).Authorized,"offline lease cannot extend beyond signed validity");
Check(!gate.Offline(now.AddMinutes(-4)).Authorized,"clock rollback outside tolerance fails closed");
Check(!gate.Apply(Signed(baseClaims),new string('f',32),now).Authorized,"nonce replay mismatch rejected");
Check(!gate.Apply(Signed(baseClaims with{ProductId="SIGNAL_AUTO_SCHEDULER"}),nonce,now).Authorized,"other product ID rejected even if cryptographically signed");
Check(!gate.Apply(Signed(baseClaims with{DeviceHash=new string('a',64)}),nonce,now).Authorized,"signed other-device lease cannot be imported");
Check(!gate.Apply(Signed(baseClaims with{Status="disabled"}),nonce,now).Authorized,"disabled account is rejected even with valid signature");
Check(!gate.Apply(Signed(baseClaims with{Version=0}),nonce,now).Authorized,"invalid license revision denied");
Check(!gate.Apply(Signed(baseClaims with{LeaseUntilMs=now.AddHours(3).ToUnixTimeMilliseconds()}),nonce,now).Authorized,"signed lease above 1h maximum rejected");
Check(!gate.Apply(Signed(baseClaims with{ExpiresMs=now.AddMinutes(1).ToUnixTimeMilliseconds(),LeaseUntilMs=now.AddMinutes(2).ToUnixTimeMilliseconds()}),nonce,now).Authorized,
 "lease beyond card expiry rejected");
Check(!gate.Apply(Signed(baseClaims with{CheckedMs=now.AddHours(3).ToUnixTimeMilliseconds()}),nonce,now).Authorized,"future server time cannot spoof online lease");
Check(!gate.Apply(Signed(baseClaims with{ExpiresMs=now.AddSeconds(-1).ToUnixTimeMilliseconds()}),nonce,now).Authorized,"expired card cannot grant lease");
Check(!gate.Apply(Signed(baseClaims with{LicenseId="invalid"}),nonce,now).Authorized,"malformed license GUID rejected");
Check(!gate.Apply(Signed(baseClaims,otherSigner),nonce,now).Authorized,"signature from different Telegram product key rejected");
var tampered=Signed(baseClaims) with {Claims=baseClaims with {ExpiresMs=now.AddYears(5).ToUnixTimeMilliseconds()}};
Check(!gate.Apply(tampered,nonce,now).Authorized,"tampering signed expiry invalidates signature");
var tamperedSig=Signed(baseClaims) with{Signature="untrustedtoken"};
Check(!gate.Apply(tamperedSig,nonce,now).Authorized,"malformed detached signature fails closed");
var nextClaims=baseClaims with{CheckedMs=now.AddSeconds(15).ToUnixTimeMilliseconds(),LeaseUntilMs=now.AddMinutes(45).ToUnixTimeMilliseconds(),Nonce=new string('c',32)};
Check(gate.Apply(Signed(nextClaims),nextClaims.Nonce,now.AddSeconds(15)).Authorized,
 "new legitimate signed revision may replace cached lease");
Check(gate.Apply(Signed(baseClaims),nonce,now).Code=="STALE_REPLAY",
 "older otherwise valid signed response cannot roll server-epoch anchor backwards");
Reject<ArgumentException>(()=>new SignedLicenseVerifier("",device),"missing pinned signing key cannot initialize verification");
Reject<ArgumentException>(()=>new SignedLicenseVerifier(publicKey,"raw-device-identifier"),"raw device label rather than SHA-256 digest rejected");
Reject<CryptographicException>(()=>new SignedLicenseVerifier(Convert.ToBase64String(RandomNumberGenerator.GetBytes(40)),device),
 "invalid signing key blob fails closed");
Reject<ArgumentException>(()=>new LicenseHttpClient(new HttpClient(),new Uri("http://license.example.com"),gate,device),
 "plaintext HTTP license endpoint prohibited");
Reject<ArgumentException>(()=>new LicenseHttpClient(new HttpClient(),new Uri("https://name:password@license.example.com"),gate,device),
 "URLs containing embedded credentials prohibited");

var handled=0;string? capturedCard=null;
using var mockHttp=new HttpClient(new ReplyHandler(async request=>{
 handled++;
 var serialized=await request.Content!.ReadAsStringAsync();
 using var doc=JsonDocument.Parse(serialized);
 capturedCard=doc.RootElement.GetProperty("card").GetString();
 var currentNonce=doc.RootElement.GetProperty("nonce").GetString()!;
 var c=baseClaims with{Nonce=currentNonce,CheckedMs=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    LeaseUntilMs=DateTimeOffset.UtcNow.AddMinutes(59).ToUnixTimeMilliseconds()};
 var response=Signed(c);
 return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(response,
    new JsonSerializerOptions(JsonSerializerDefaults.Web)),Encoding.UTF8,"application/json")};
}));
var client=new LicenseHttpClient(mockHttp,new Uri("https://telegram-license.test"),
 new SignedLicenseVerifier(publicKey,device),device);
var card="TG-"+string.Join("-",Enumerable.Repeat("A1B2C3D4",4));
var answer=await client.CheckAsync(card,true);
Check(answer.Authorized&&handled==1,"HTTPS client verifies actual mock server signature and fresh nonce");
Check(capturedCard==card,"card only submitted inside HTTPS POST request body");
Check((await client.CheckAsync(card,false)).Authorized,"authorized validation submits new nonce and verifies response");
Check((await client.CheckAsync("short",false)).Code=="NO_CARD"&&handled==2,
 "invalid card never reaches transport");
using var deniedHttp=new HttpClient(new ReplyHandler(_=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden))));
var deniedClient=new LicenseHttpClient(deniedHttp,new Uri("https://telegram-license.test"),gate,device);
Check((await deniedClient.CheckAsync(card,false)).Code=="REMOTE_DENIED","remote disabled result fails closed");
Check(!gate.Offline(DateTimeOffset.UtcNow).Authorized,"explicit remote revocation invalidates cached signed offline lease");
using var unavailable=new HttpClient(new ReplyHandler(_=>throw new HttpRequestException("not logged")));
var offlineClient=new LicenseHttpClient(unavailable,new Uri("https://telegram-license.test"),gate,device);
Check((await offlineClient.CheckAsync(card,false)).Code=="CHECK_UNAVAILABLE",
 "network failure never manufactures an authorized signed lease");
var dir=Path.Combine(Path.GetTempPath(),"ms001-license-device-"+Guid.NewGuid().ToString("N"));
try
{
 var file=Path.Combine(dir,"device.seed");
 var stable=InstallationDeviceId.LoadOrCreate(file);
 Check(stable.Length==64&&stable.All(x=>char.IsAsciiHexDigitLower(x)),"random installation pseudonym is lowercase SHA-256 only");
 Check(InstallationDeviceId.LoadOrCreate(file)==stable,"installation identity is stable across simulated restarts");
 Check(new FileInfo(file).Length==32,"device seed stores 32 random bytes, not machine hardware IDs");
 File.WriteAllBytes(file,new byte[]{1,2,3});
 Reject<InvalidDataException>(()=>InstallationDeviceId.LoadOrCreate(file),"damaged installation ID fails closed rather than silently rebinding");
}
finally{try{Directory.Delete(dir,true);}catch(IOException){}}

// Step26: signed public key rotation, no user-provided public keys, offline clock rollback
// and attempted tampering fail closed rather than retaining a previously verified lease.
var legacySpki=Convert.ToBase64String(signing.ExportSubjectPublicKeyInfo());
var newSpki=Convert.ToBase64String(otherSigner.ExportSubjectPublicKeyInfo());
var multi=new SignedLicenseVerifier(new Dictionary<string,string>{
 ["tg-v1"]=legacySpki,["tg-v2"]=newSpki
},device);
var signedOld=Signed(baseClaims) with {KeyId="tg-v1"};
Check(multi.Apply(signedOld,nonce,now).Authorized,"pinned old key remains accepted during two-key rotation");
var signedNew=Signed(baseClaims,otherSigner) with {KeyId="tg-v2"};
Check(multi.Apply(signedNew,nonce,now).Authorized,"independently pinned new publisher signing key accepted");
Check(!multi.Apply(signedNew with {KeyId="untrusted-publisher"},nonce,now).Authorized,
 "untrusted key ID from response cannot expand the publisher keyring");
Check(!multi.Offline(now).Authorized,
 "untrusted key ID revokes any in-memory cached offline gate");
Check(!multi.Apply(Signed(baseClaims,otherSigner) with {KeyId="tg-v1"},nonce,now).Authorized,
 "key ID spoof cannot make a new signing key verify as the old one");
Check(!multi.Offline(now).Authorized,
 "bad signature clears old cached offline authorization");
Reject<ArgumentException>(()=>new SignedLicenseVerifier(new Dictionary<string,string>{
 ["tg-v1"]=legacySpki,["tg-v2"]=newSpki,["third"]=legacySpki
},device),"more than two developer pinned keys rejected");
Reject<ArgumentException>(()=>new SignedLicenseVerifier(new Dictionary<string,string>{
 ["tg-v1"]=legacySpki,["evil/key"]=newSpki
},device),"invalid signing key identifiers rejected");

var rollbackGuard=new SignedLicenseVerifier(publicKey,device);
Check(rollbackGuard.Apply(Signed(baseClaims),nonce,now).Authorized,
 "signed lease anchors monotonic counter when validated");
Check(rollbackGuard.Offline(now.AddMinutes(40)).Authorized,
 "signed still-valid clock observation allowed");
Check(!rollbackGuard.Offline(now.AddMinutes(20)).Authorized,
 "rolling device clock back after later observation cannot re-extend offline time");
Check(!rollbackGuard.Offline(now.AddHours(2)).Authorized,
 "clock cannot exceed signed offline expiry even with in-process cache");
var invalidGate=new SignedLicenseVerifier(publicKey,device);
Check(invalidGate.Apply(Signed(baseClaims),nonce,now).Authorized,
 "second gate independently signs original claims");
Check(!invalidGate.Apply(Signed(baseClaims) with{
 Claims=baseClaims with{ExpiresMs=baseClaims.ExpiresMs+86400000}
},nonce,now).Authorized,"tampered server response explicitly rejected");
Check(!invalidGate.Offline(now).Authorized,
 "tampered online license response invalidates prior cached offline lease");
Console.WriteLine("Step26 publisher key-rotation and offline rollback guard tests completed.");

Console.WriteLine($"SUMMARY: {count} Step25 signed licensing client assertions passed; zero real Cloudflare/Signal modifications.");
sealed class ReplyHandler(Func<HttpRequestMessage,Task<HttpResponseMessage>> handle):HttpMessageHandler
{
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
     =>handle(request);
}
