using Mosen.GroupScheduler.Telegram;

// This is a real native TDLib smoke. It never initiates a Telegram account login,
// never retrieves user chats and never submits any user or group message.
if(!OperatingSystem.IsWindows()||Environment.Is64BitProcess==false)
  throw new PlatformNotSupportedException("Native TDLib smoke requires Windows x64.");
using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(30));
await using var receiver=new TdlibDispatcher(new NativeTdlibTransport());
int client=receiver.RegisterClient();
if(client<=0)throw new InvalidDataException("Official TDLib returned invalid client id.");
receiver.Start();
TdlibEnvelope result=await receiver.SendControlAsync(
  client,TdlibControlCommand.GetVersion,TimeSpan.FromSeconds(20),cancel.Token);
if(result.Type!="optionValueString"||
   !result.Body.TryGetProperty("value",out var version)||
   version.ValueKind!=System.Text.Json.JsonValueKind.String||
   string.IsNullOrWhiteSpace(version.GetString()))
  throw new InvalidDataException("Native TDLib did not return an actual version.");
Console.WriteLine("PASS: real win-x64 tdjson.dll loaded, native version="+version.GetString()+
  "; no account credentials, chat data or messages used.");
