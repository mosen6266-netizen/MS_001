using System.Globalization;
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
    [property:JsonPropertyName("signature")] string Signature);
public sealed record LicenseGateDecision(bool Authorized,string Code,DateTimeOffset? LeaseExpiresUtc);
/// <summary>Only a detached P-256 signature over fixed canonical claim fields grants access.
/// Worker signing private key and card pepper are NEVER included in the client build.</summary>
public sealed class SignedLicenseVerifier
{
    public const string ProductId="MOSEN_TELEGRAM_SCHEDULER";
    private readonly byte[] _publicKey;
    private readonly string _deviceHash;
    private readonly object _sync=new();
    private long _lastServerClockMs;
    private LicenseEnvelope? _lastVerified;
    public SignedLicenseVerifier(string publicKeySpkiBase64,string deviceHash)
    {
        if(string.IsNullOrWhiteSpace(publicKeySpkiBase64))throw new ArgumentException("Public key not provisioned.");
        if(deviceHash is null||deviceHash.Length!=64||deviceHash.Any(c=>c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new ArgumentException("Device identity must be a SHA-256 hex digest.");
        _publicKey=Convert.FromBase64String(publicKeySpkiBase64);
        using var key=ECDsa.Create();key.ImportSubjectPublicKeyInfo(_publicKey,out var used);
        if(used!=_publicKey.Length||key.KeySize!=256)throw new CryptographicException("Invalid P256 signing public key.");
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
        lock(_sync){_lastVerified=null;}
    }
    public LicenseGateDecision Apply(LicenseEnvelope? envelope,string requestedNonce,DateTimeOffset now)
    {
        if(envelope?.Claims is not { } c||!envelope.Ok){Invalidate();return new(false,"NO_SIGNED_LEASE",null);}
        if(requestedNonce is null||requestedNonce.Length!=32||requestedNonce.Any(ch=>
           ch is not (>= '0' and <= '9' or >= 'a' and <= 'f'))||c.Nonce!=requestedNonce)
            return new(false,"NONCE_MISMATCH",null);
        if(c.ProductId!=ProductId||c.DeviceHash!=_deviceHash||c.Status!="valid"||
           !Guid.TryParseExact(c.LicenseId,"D",out _)||c.Version<1)
            return new(false,"PRODUCT_DEVICE_OR_STATE",null);
        var epoch=now.ToUnixTimeMilliseconds();
        if(c.CheckedMs<=0||c.CheckedMs>epoch+120000||epoch<c.CheckedMs-120000||
            c.ExpiresMs<=c.CheckedMs||c.LeaseUntilMs>c.ExpiresMs||
            c.LeaseUntilMs>c.CheckedMs+3600000||c.LeaseUntilMs<=epoch)
            return new(false,"LEASE_WINDOW_INVALID",null);
        try
        {
            using var verifier=ECDsa.Create();verifier.ImportSubjectPublicKeyInfo(_publicKey,out _);
            var signature=DecodeBase64Url(envelope.Signature);
            if(signature.Length!=64||!verifier.VerifyData(Encoding.UTF8.GetBytes(Canonical(c)),signature,
                HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                return new(false,"SIGNATURE_INVALID",null);
        }
        catch(Exception e) when(e is FormatException or CryptographicException)
        {return new(false,"SIGNATURE_INVALID",null);}
        lock(_sync)
        {
            if(c.CheckedMs<_lastServerClockMs)return new(false,"STALE_REPLAY",null);
            _lastServerClockMs=c.CheckedMs;
            _lastVerified=envelope;
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
            if(epoch<claims.CheckedMs-120000||epoch>=claims.LeaseUntilMs||epoch>=claims.ExpiresMs)
                return new(false,"OFFLINE_LEASE_EXPIRED",null);
            return new(true,"SIGNED_OFFLINE_LEASE",DateTimeOffset.FromUnixTimeMilliseconds(claims.LeaseUntilMs));
        }
    }
}
