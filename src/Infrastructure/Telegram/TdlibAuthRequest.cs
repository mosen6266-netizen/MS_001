using System.Text.Json;
using System.Security.Cryptography;

namespace Mosen.GroupScheduler.Telegram;

/// <summary>
/// Configuration must come from trusted local storage. It is never passed to the WebView,
/// exported in scripts, or written to telemetry. Caller must clear its key when no longer used.
/// </summary>
public sealed class TdlibAuthConfiguration
{
    public int ApiId { get; }
    public string ApiHash { get; }
    public string DatabaseDirectory { get; }
    public string FilesDirectory { get; }
    public byte[] DatabaseKey { get; }
    public bool TestDatacenter { get; }
    public TdlibAuthConfiguration(int apiId,string apiHash,string databaseDirectory,
        string filesDirectory,byte[] databaseKey,bool testDatacenter=false)
    {
        if(apiId<=0 || string.IsNullOrWhiteSpace(apiHash) || apiHash.Length!=32 ||
            !apiHash.All(Uri.IsHexDigit))throw new ArgumentException("Application API credentials are invalid.");
        if(string.IsNullOrWhiteSpace(databaseDirectory)||!Path.IsPathFullyQualified(databaseDirectory) ||
            string.IsNullOrWhiteSpace(filesDirectory)||!Path.IsPathFullyQualified(filesDirectory))
            throw new ArgumentException("TDLib session directories must be absolute local paths.");
        if(databaseKey is null || databaseKey.Length!=32)throw new ArgumentException("A 32-byte TDLib database key is required.");
        ApiId=apiId;ApiHash=apiHash;DatabaseDirectory=Path.GetFullPath(databaseDirectory);
        FilesDirectory=Path.GetFullPath(filesDirectory);DatabaseKey=databaseKey;
        TestDatacenter=testDatacenter;
    }
    public override string ToString()=>"TdlibAuthConfiguration(REDACTED)";
}

/// <summary>Factory-only TDLib authorization commands; no arbitrary JSON exposed to callers.</summary>
public sealed class TdlibAuthRequest
{
    private readonly string _type;
    private readonly Dictionary<string,object?> _fields;
    private TdlibAuthRequest(string type,Dictionary<string,object?> fields){_type=type;_fields=fields;}
    public override string ToString()=>"TdlibAuthRequest("+_type+"; payload=REDACTED)";
    internal string Build(string correlation)
    {
        if(string.IsNullOrEmpty(correlation)||correlation.Length>80)throw new ArgumentException("Bad correlation.");
        var payload=new Dictionary<string,object?>(_fields){["@type"]=_type,["@extra"]=correlation};
        return JsonSerializer.Serialize(payload);
    }
    public static TdlibAuthRequest Configure(TdlibAuthConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new("setTdlibParameters",new(){
            ["api_id"]=config.ApiId,["api_hash"]=config.ApiHash,
            ["database_directory"]=config.DatabaseDirectory,["files_directory"]=config.FilesDirectory,
            ["database_encryption_key"]=Convert.ToBase64String(config.DatabaseKey),
            ["use_test_dc"]=config.TestDatacenter,
            ["use_file_database"]=true,["use_chat_info_database"]=true,
            ["use_message_database"]=false,["use_secret_chats"]=false,
            ["system_language_code"]="zh-CN",["device_model"]="Windows PC",
            ["system_version"]="",["application_version"]="1.0.0"
        });
    }
    public static TdlibAuthRequest RequestQr()=>
        new("requestQrCodeAuthentication",new(){["other_user_ids"]=Array.Empty<long>()});
    public static TdlibAuthRequest RefreshQr()=>
        new("getAuthorizationState",new()); // TDLib owns QR link refresh; do not start duplicate QR auth
    public static TdlibAuthRequest Phone(string phone) =>
        new("setAuthenticationPhoneNumber",new(){["phone_number"]=phone,["settings"]=null});
    public static TdlibAuthRequest Code(string value) =>
        new("checkAuthenticationCode",new(){["code"]=value});
    public static TdlibAuthRequest EmailAddress(string value) =>
        new("setAuthenticationEmailAddress",new(){["email_address"]=value});
    public static TdlibAuthRequest EmailCode(string value) =>
        new("checkAuthenticationEmailCode",new(){["code"]=new Dictionary<string,object?>{
            ["@type"]="emailAddressAuthenticationCode",["code"]=value}});
    public static TdlibAuthRequest Password(string password) =>
        new("checkAuthenticationPassword",new(){["password"]=password});
    public static TdlibAuthRequest Registration(string first,string last) =>
        new("registerUser",new(){["first_name"]=first,["last_name"]=last,
            ["disable_notification"]=true});
}
