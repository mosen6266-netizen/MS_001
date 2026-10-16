using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace Mosen.GroupScheduler.Licensing;
/// <summary>Optional transport when a separately configured HTTPS Telegram license service
/// exists. Fail-closed when missing; never log a plaintext card or license response.</summary>
public sealed class LicenseHttpClient
{
    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly SignedLicenseVerifier _verifier;
    private readonly string _device;
    public LicenseHttpClient(HttpClient http,Uri endpoint,SignedLicenseVerifier verifier,string deviceHash)
    {
        _http=http??throw new ArgumentNullException(nameof(http));
        _verifier=verifier??throw new ArgumentNullException(nameof(verifier));
        if(endpoint is null||endpoint.Scheme!="https"||endpoint.UserInfo.Length!=0||
            endpoint.Query.Length!=0||endpoint.Fragment.Length!=0)
            throw new ArgumentException("Only fixed HTTPS authorization origins are supported.");
        _endpoint=endpoint;
        _device=deviceHash;
    }
    public async Task<LicenseGateDecision> CheckAsync(string card,bool activate,CancellationToken cancel=default)
    {
        if(string.IsNullOrWhiteSpace(card)||card.Length!=38)return new(false,"NO_CARD",null);
        // New nonce for every check. Unauthenticated packets never unlock the local gate.
        string nonce=Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var target=new Uri(_endpoint,activate?"/v1/license/activate":"/v1/license/check");
        try
        {
            using var request=new HttpRequestMessage(HttpMethod.Post,target){
                Content=JsonContent.Create(new{card,deviceHash=_device,nonce})
            };
            using var response=await _http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel);
            if(!response.IsSuccessStatusCode){_verifier.Invalidate();return new(false,"REMOTE_DENIED",null);}
            if(response.Content.Headers.ContentLength is > 4096)return new(false,"INVALID_RESPONSE",null);
            await using var stream=await response.Content.ReadAsStreamAsync(cancel);
            var envelope=await JsonSerializer.DeserializeAsync<LicenseEnvelope>(stream,
                new JsonSerializerOptions(JsonSerializerDefaults.Web),cancel);
            return _verifier.Apply(envelope,nonce,DateTimeOffset.UtcNow);
        }
        catch(Exception e) when(e is HttpRequestException or TaskCanceledException or
            JsonException or IOException)
        {return new(false,"CHECK_UNAVAILABLE",null);}
    }
}
