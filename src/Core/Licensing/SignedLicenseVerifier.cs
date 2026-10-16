using System.Globalization;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mosen.GroupScheduler.Licensing;

public sealed record LicenseClaims(
    [property:JsonPropertyName("productId")] string ProductId,
    [property:JsonPropertyName("licenseId")] string LicenseId,
    [property:JsonPropertyName("deviceHash")] string DeviceHash,
    [property:JsonPropertyName("expiresMs")] long ExpiresMs,
    [property:JsonPropertyName("checkedMs")] long CheckedMs,
    [property:JsonPropertyName("leaseUntilMs")] long LeaseUntilMs,
    [property:JsonPropertyName("version")] int Version,
    [property:JsonPropertyName("status")] string Status,
    [property:JsonPropertyName("nonce")] string Nonce);
public sealed record LicenseEnvelope(
    [property:JsonPropertyName("ok")] bool Ok,
    [property:JsonPropertyName("claims")] LicenseClaims Claims,
    [property:JsonPropertyName("signature")] string Signature,
    [property:JsonPropertyName("keyId")] string? KeyId=null);
public sealed record LicenseGateDecision(bool Authorized,string Code,DateTimeOffset? LeaseExpiresUtc);
/// <summary>Only a detached P-256 signature over fixed canonical claim fields grants access.
/// Worker signing private key and card pepper are NEVER included in the client build.</summary>
public sealed class SignedLicenseVerifier
{
    public const string ProductId="MOSEN_TELEGRAM_SCHEDULER";
    private readonly IReadOnlyDictionary<string,byte[]> _trustedKeys;
    private readonly string _deviceHash;
    private readonly object _sync=new();
    private long _lastServerClockMs;
    private long _lastObservedWallMs;
    private long _verifiedStamp;
    private LicenseEnvelope? _lastVerified;
    public SignedLicenseVerifier(string publicKeySpkiBase64,string deviceHash)
        :this(new Dictionary<string,string>{{"tg-v1",publicKeySpkiBase64}},deviceHash){}
    /// <summary>Trusted key IDs MUST be embedded by the publisher; never imported
    /// from a license response. Two staged IDs allow zero-downtime P-256 rotation.</summary>
    public SignedLicenseVerifier(IReadOnlyDictionary<string,string> trustedPublisherKeys,string deviceHash)
    {
        if(trustedPublisherKeys is null||trustedPublisherKeys.Count is <1 or >2)
            throw new ArgumentException("One or two pinned public keys required.");
        if(deviceHash is null||deviceHash.Length!=64||deviceHash.Any(c=>c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new ArgumentException("Device identity must be a SHA-256 hex digest.");
        var validated=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        foreach(var (id,value) in trustedPublisherKeys)
        {
            if(id.Length is <3 or >32||id.Any(c=>!(char.IsAsciiLetterOrDigit(c)||c=='-'))||
               string.IsNullOrWhiteSpace(value))throw new ArgumentException("Invalid fixed signing key ID.");
            var bytes=Convert.FromBase64String(value);
            using var key=ECDsa.Create();key.ImportSubjectPublicKeyInfo(bytes,out var used);
            if(used!=bytes.Length||key.KeySize!=256)throw new CryptographicException("Invalid P256 signing public key.");
            validated.Add(id,bytes);
        }
        _trustedKeys=validated;
        _deviceHash=deviceHash;
    }
    public static string Canonical(LicenseClaims claim)=>string.Join("\n",new[]{
        claim.ProductId,claim.LicenseId,claim.DeviceHash,
        claim.ExpiresMs.ToString(CultureInfo.InvariantCulture),
        claim.CheckedMs.ToString(CultureInfo.InvariantCulture),
        claim.LeaseUntilMs.ToString(CultureInfo.InvariantCulture),
        claim.Version.ToString(CultureInfo.InvariantCulture),claim.Status,claim.Nonce
    });
    private static byte[] DecodeBase64Url(string text)
    {
        if(string.IsNullOrWhiteSpace(text)||text.Length>256||text.Any(c=>
            c is not (>= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_')))
            throw new FormatException("Invalid detached signature encoding.");
        return Convert.FromBase64String(text.Replace('-','+').Replace('_','/')
            .PadRight((text.Length+3)/4*4,'='));
    }
    public void Invalidate()
    {
        lock(_sync){_lastVerified=null;_verifiedStamp=0;}
    }
    private LicenseGateDecision Reject(string code){Invalidate();return new(false,code,null);}
    public LicenseGateDecision Apply(LicenseEnvelope? envelope,string requestedNonce,DateTimeOffset now)
    {
        if(envelope?.Claims is not { } c||!envelope.Ok)return Reject("NO_SIGNED_LEASE");
        if(requestedNonce is null||requestedNonce.Length!=32||requestedNonce.Any(ch=>
           ch is not (>= '0' and <= '9' or >= 'a' and <= 'f'))||c.Nonce!=requestedNonce)
            return Reject("NONCE_MISMATCH");
        if(c.ProductId!=ProductId||c.DeviceHash!=_deviceHash||c.Status!="valid"||
           !Guid.TryParseExact(c.LicenseId,"D",out _)||c.Version<1)
            return Reject("PRODUCT_DEVICE_OR_STATE");
        var epoch=now.ToUnixTimeMilliseconds();
        if(c.CheckedMs<=0||c.CheckedMs>epoch+120000||epoch<c.CheckedMs-120000||
            c.ExpiresMs<=c.CheckedMs||c.LeaseUntilMs>c.ExpiresMs||
            c.LeaseUntilMs>c.CheckedMs+3600000||c.LeaseUntilMs<=epoch)
            return Reject("LEASE_WINDOW_INVALID");
        var keyId=envelope.KeyId??"tg-v1";
        if(!_trustedKeys.TryGetValue(keyId,out var pinnedKey))return Reject("UNPINNED_KEY");
        try
        {
            using var verifier=ECDsa.Create();verifier.ImportSubjectPublicKeyInfo(pinnedKey,out _);
            var signature=DecodeBase64Url(envelope.Signature);
            if(signature.Length!=64||!verifier.VerifyData(Encoding.UTF8.GetBytes(Canonical(c)),signature,
                HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                return Reject("SIGNATURE_INVALID");
        }
        catch(Exception e) when(e is FormatException or CryptographicException)
        {return Reject("SIGNATURE_INVALID");}
        lock(_sync)
        {
            if(c.CheckedMs<_lastServerClockMs)return Reject("STALE_REPLAY");
            _lastServerClockMs=c.CheckedMs;
            _lastVerified=envelope;
            _lastObservedWallMs=Math.Max(_lastObservedWallMs,epoch);
            _verifiedStamp=Stopwatch.GetTimestamp();
        }
        return new(true,"VALID",DateTimeOffset.FromUnixTimeMilliseconds(c.LeaseUntilMs));
    }
    /// <summary>Signed 1-hour maximum lease, no user-controlled offline extension.
    /// Missing internet does NOT manufacture a new lease or excuse clock rollback.</summary>
    public LicenseGateDecision Offline(DateTimeOffset now)
    {
        lock(_sync)
        {
            if(_lastVerified?.Claims is not { } claims)return new(false,"NOT_ACTIVATED",null);
            var epoch=now.ToUnixTimeMilliseconds();
            // Wall-clock rollback cannot create extra offline time; Stopwatch measures
            // elapsed process time independently. Once observed a later wall time, do
            // not allow the clock to be set backward to stretch a signed lease.
            if(epoch<_lastObservedWallMs-120000||epoch<claims.CheckedMs-120000||
               epoch>=claims.LeaseUntilMs||epoch>=claims.ExpiresMs||
               _verifiedStamp==0||
               Stopwatch.GetElapsedTime(_verifiedStamp).TotalMilliseconds>=
                 claims.LeaseUntilMs-claims.CheckedMs)
                return new(false,"OFFLINE_LEASE_EXPIRED",null);
            _lastObservedWallMs=Math.Max(_lastObservedWallMs,epoch);
            return new(true,"SIGNED_OFFLINE_LEASE",DateTimeOffset.FromUnixTimeMilliseconds(claims.LeaseUntilMs));
        }
    }
}
