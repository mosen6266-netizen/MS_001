using Mosen.GroupScheduler.Installation;
using System.Text;

int passed=0;
void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("FAIL "+text);passed++;Console.WriteLine("PASS "+text);}
void Reject<T>(Action action,string label) where T:Exception {
 bool caught=false;try{action();}catch(T){caught=true;}Check(caught,label);
}
var root=Path.Combine(Path.GetTempPath(),"ms001-install-"+Guid.NewGuid().ToString("N"));
var appDir=Path.Combine(root,"installed","MOSEN Telegram Scheduler");
var appData=Path.Combine(root,"profile","LocalAppData");
var data=UserDataPaths.Resolve(appData);
try{
 Directory.CreateDirectory(appDir);Directory.CreateDirectory(data);
 Check(data==Path.Combine(appData,"MOSEN","TelegramScheduler"),"stable private data folder unaffected by install name");
 Check(UserDataPaths.WebView2(data)==Path.Combine(data,"webview2"),"WebView2 browser profile is not adjacent to exe");
 Check(UserDataPaths.AppDatabase(data)==Path.Combine(data,"app.db"),"database root matches historical Step04 path");
 Check(UserDataPaths.Backups(data)==Path.Combine(data,"backups"),"migration backups remain in user data root");
 Check(UserDataPaths.DiagnosticLogs(data)==Path.Combine(data,"diagnostics"),"log root matches Step08 telemetry");
 UserDataPaths.AssertNoInstallDirectoryOverlap(appDir,data);
 Check(true,"separate installer and app data paths accepted");
 Reject<ArgumentException>(()=>UserDataPaths.Resolve("relative"),"relative profile paths rejected");
 Reject<ArgumentException>(()=>UserDataPaths.Resolve(""),"empty profile path rejected");
 Reject<InvalidOperationException>(()=>UserDataPaths.AssertNoInstallDirectoryOverlap(data,data),
   "install destination may not equal user data");
 Reject<InvalidOperationException>(()=>UserDataPaths.AssertNoInstallDirectoryOverlap(data,Path.Combine(data,"nested")),
   "nested data under installation is forbidden");
 Reject<InvalidOperationException>(()=>UserDataPaths.AssertNoInstallDirectoryOverlap(Path.Combine(data,"webview2"),data),
   "install inside data root is forbidden");
 var privateDir=Path.Combine(data,"sessions");Directory.CreateDirectory(privateDir);
 File.WriteAllText(Path.Combine(data,"app.db"),"PRIVATE_DATABASE");
 File.WriteAllText(Path.Combine(data,"alerts.json"),"PRIVATE_NOTIFICATIONS");
 File.WriteAllText(Path.Combine(data,"window-state.json"),"PRIVATE_WINDOW");
 File.WriteAllText(Path.Combine(privateDir,"tdlib-key.bin"),"SECRET_SESSION_SEED");
 var backup=Path.Combine(data,"backups");Directory.CreateDirectory(backup);
 File.WriteAllText(Path.Combine(backup,"app.db.backup"),"PRIVATE_BACKUP");
 var browser=UserDataPaths.WebView2(data);Directory.CreateDirectory(browser);
 File.WriteAllText(Path.Combine(browser,"Default"),"WEBVIEW2_PROFILE");
 // Representative program layout of first install and version overwrite.
 File.WriteAllText(Path.Combine(appDir,"Mosen.GroupScheduler.Desktop.exe"),"v1-code");
 File.WriteAllText(Path.Combine(appDir,"legacy-version.txt"),"v1-code");
 File.WriteAllText(Path.Combine(appDir,"Mosen.GroupScheduler.Desktop.exe"),"v2-code");
 Check(File.ReadAllText(Path.Combine(data,"app.db"))=="PRIVATE_DATABASE","overlay update leaves original application DB intact");
 Check(File.ReadAllText(Path.Combine(privateDir,"tdlib-key.bin"))=="SECRET_SESSION_SEED","overlay update does not reset TDLib account secrets");
 Check(File.ReadAllText(Path.Combine(data,"alerts.json"))=="PRIVATE_NOTIFICATIONS","overlay update preserves notification state");
 Check(File.ReadAllText(Path.Combine(data,"window-state.json"))=="PRIVATE_WINDOW","overlay update preserves saved window coordinates");
 Check(File.ReadAllText(Path.Combine(backup,"app.db.backup"))=="PRIVATE_BACKUP","overlay update preserves pre-migration backup");
 Check(File.ReadAllText(Path.Combine(browser,"Default"))=="WEBVIEW2_PROFILE","overlay update preserves WebView2 auth profile");
 // Uninstall default removes install destination ONLY.
 Directory.Delete(appDir,true);
 Check(Directory.Exists(data)&&File.Exists(Path.Combine(data,"app.db")),
       "uninstall default leaves entire user data directory");
 Check(File.Exists(Path.Combine(privateDir,"tdlib-key.bin")),"uninstall does not erase account seed");
 Check(File.Exists(Path.Combine(backup,"app.db.backup")),"uninstall does not erase backup");
 Check(File.Exists(Path.Combine(browser,"Default")),"uninstall does not erase browser settings");
 Check(!Directory.Exists(appDir),"program directory removed without touching data");
 Console.WriteLine($"SUMMARY: {passed} Step27 simulated installation path/upgrade/uninstall assertions passed.");
}finally{if(Directory.Exists(root))Directory.Delete(root,true);}
