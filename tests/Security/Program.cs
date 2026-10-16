using System.Security.Cryptography;
using System.Text;
using Mosen.GroupScheduler.Core.Security;
using Mosen.GroupScheduler.Core.Telemetry;

int passed=0;
void Check(bool condition,string label)
{
    if(!condition)throw new InvalidOperationException("FAIL: "+label);
    passed++;Console.WriteLine("PASS "+label);
}
const string valid= """{"protocol":"mosen.desktop.v1","requestId":"req-1","type":"system.ping"}""";
const string prefs= """{"density":"comfortable","searchDebounceMs":200,"pageSize":20,"desktopNotifications":true}""";
string save= """{"protocol":"mosen.desktop.v1","requestId":"save-1","type":"settings.save","payload":{"expectedRevision":0,"preferences":""" + prefs + "}}";
Check(IpcRequestGuard.TryValidate(valid,out var operation)&&operation==AllowedIpcOperation.SystemPing,"ping passes exact allowlist");
Check(IpcRequestGuard.TryValidate(save,out operation)&&operation==AllowedIpcOperation.SettingsSave,"valid settings save passes exact payload check");
Check(IpcRequestGuard.TryValidate("""{"protocol":"mosen.desktop.v1","requestId":"diag-1","type":"diagnostics.get"}""",out operation)&&operation==AllowedIpcOperation.DiagnosticsGet,"read-only diagnostics command allowed");
Check(!IpcRequestGuard.TryValidate("""{"protocol":"mosen.desktop.v1","requestId":"x","type":"telegram.send"}""",out _),"Telegram network command denied");
Check(!IpcRequestGuard.TryValidate(valid.Replace("system.ping","system.ping\",\"type\":\"settings.get"),out _),"duplicate root type denied");
Check(!IpcRequestGuard.TryValidate(save.Replace("\"pageSize\":20", "\"pageSize\":20,\"pageSize\":50"),out _),"duplicate nested preference denied");
Check(!IpcRequestGuard.TryValidate(save.Replace("\"density\":\"comfortable\"", "\"density\":\"compact\",\"token\":\"BAD-SECRET\""),out _),"credential keys denied");
Check(!IpcRequestGuard.TryValidate(save.Replace("\"searchDebounceMs\":200","\"searchDebounceMs\":1"),out _),"invalid debounce denied");
Check(!IpcRequestGuard.TryValidate(save.Replace("\"expectedRevision\":0","\"expectedRevision\":-1"),out _),"negative revision denied");
Check(!IpcRequestGuard.TryValidate(save.Replace("\"pageSize\":20","\"pageSize\":300"),out _),"too large page denied");
Check(!IpcRequestGuard.TryValidate(valid.Replace("\"requestId\":\"req-1\"","\"requestId\":\"../../something\""),out _),"path-like request ID denied");
Check(!IpcRequestGuard.TryValidate(valid.Replace("}",",\"payload\":{\"hack\":1}}"),out _),"surplus payload denied");
Check(!IpcRequestGuard.TryValidate(new string('X',8193),out _),"oversize message denied");
Check(!IpcRequestGuard.TryValidate("{bad json",out _),"malformed JSON denied");

string tmp=Path.Combine(Path.GetTempPath(),"ms001-security-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tmp);
try
{
    var diagnostics=new SafeDiagnostics(tmp, maxBytes:2048);
    string trap="INPUT-DO-NOT-LOG +12025550123 @testtoken password=topsecret";
    bool everyWrite=true;
    for(int i=0;i<1200;i++) everyWrite&=diagnostics.Record(i%2==0?DiagnosticEvent.SettingsSaved:DiagnosticEvent.SettingsConflict);
    Check(everyWrite,"1200 fixed-code events persisted and rotated");
    Check(diagnostics.Record(DiagnosticEvent.RecoverableError,
        FailureClassifier.Classify(new IOException(trap))),"record categorized I/O without exception text");
    Check(diagnostics.Record(DiagnosticEvent.FatalError,
        FailureClassifier.Classify(new InvalidOperationException(trap))),"record categorized unexpected crash");
    var snapshot=diagnostics.Snapshot("ready");
    Check(snapshot.Events==1202 && snapshot.Failures==1 && snapshot.Warnings==601,"health counters stable and bounded");
    string combined=string.Join("\n",Directory.GetFiles(tmp).Select(File.ReadAllText));
    Check(!combined.Contains(trap) && !combined.Contains("password=") && !combined.Contains("+12025550123"),
        "logs have no sensitive input values, exception messages or phone content");
    Check(Directory.GetFiles(tmp).Length==2,"file rollover keeps only current+previous");
    Check(Directory.GetFiles(tmp).Sum(f=>new FileInfo(f).Length)<5*1024,
        "files rotate before unbounded growth");
    Check(combined.Contains("\"code\"")&&combined.Contains("\"category\""),"fixed code-only JSONL schema");
    Check(FailureClassifier.Classify(new IOException(trap))==FailureCategory.FileSystem,"exception safely categorized");

    if(OperatingSystem.IsWindows())
    {
        byte[] secret=Encoding.UTF8.GetBytes("test-only-ephemeral-secret");
        string protectedEnvelope=DpapiProtector.Protect(secret);
        Check(!protectedEnvelope.Contains("test-only-ephemeral-secret"),"DPAPI does not expose plaintext");
        byte[] recovered=DpapiProtector.Unprotect(protectedEnvelope);
        Check(CryptographicOperations.FixedTimeEquals(secret,recovered),"Windows DPAPI user round trip");
        CryptographicOperations.ZeroMemory(secret);
        CryptographicOperations.ZeroMemory(recovered);
        bool broken=false;
        try{DpapiProtector.Unprotect(protectedEnvelope+"broken");}
        catch(CryptographicException){broken=true;}
        Check(broken,"tampered DPAPI envelope rejected");
    }
    else
    {
        bool denied=false;
        try{DpapiProtector.Protect(Encoding.UTF8.GetBytes("test"));}
        catch(PlatformNotSupportedException){denied=true;}
        Check(denied,"non-Windows DPAPI use fails closed");
    }
    Console.WriteLine($"SUMMARY: {passed} security/diagnostics assertions passed; no live Telegram sessions or messages.");
}
finally
{
    try{Directory.Delete(tmp,recursive:true);}
    catch(IOException){/* CI only */ }
}
