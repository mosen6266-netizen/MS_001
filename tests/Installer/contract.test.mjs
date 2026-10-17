import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const read=path=>readFileSync(new URL(path,import.meta.url),"utf8");
const setup=read("../../installer/Setup.iss");
const build=read("../../build/Build-Installer.ps1");
const cleanup=read("../../build/Remove-LocalUserData.ps1");
const desktop=read("../../apps/desktop/MainWindow.xaml.cs");
const project=read("../../apps/desktop/Mosen.GroupScheduler.Desktop.csproj");
const cs=read("../../src/Core/Installation/UserDataPaths.cs");

test("Step27: installer is stable per-user and never reuses Signal identity",()=>{
 assert.match(setup,/AppId=\{\{[0-9A-F-]{36}\}/i);
 assert.match(setup,/PrivilegesRequired=lowest/);
 assert.match(setup,/DefaultDirName=\{localappdata\}\\Programs\\MOSEN\\TelegramScheduler/);
 assert.match(setup,/UsePreviousAppDir=yes/);
 assert.match(setup,/DisableDirPage=yes/);
 assert.match(setup,/ArchitecturesAllowed=x64compatible/);
 assert.match(setup,/AppMutex=Local\\MOSEN\.GroupScheduler\.Desktop\.Singleton/);
 assert.doesNotMatch(setup,/DefaultDirName=\{localappdata\}\\Signal/);
});
test("Step27: install payload includes all program files, icon and both shortcuts",()=>{
 assert.match(setup,/Source: "\{#PublishDir\}\\\*"; DestDir: "\{app\}"; Flags: ignoreversion recursesubdirs createallsubdirs/);
 assert.match(setup,/SetupIconFile=\{#BrandingFile\}/);
 assert.match(project,/<ApplicationIcon>\.\.\/\.\.\/assets\/branding\/generated\/app\.ico<\/ApplicationIcon>/);
 assert.match(setup,/Name: "\{autoprograms\}\\\{#ProductName\}"/);
 assert.match(setup,/Name: "\{autodesktop\}\\\{#ProductName\}"/);
 assert.match(setup,/Flags: checkedonce/);
 assert.match(setup,/UninstallDisplayIcon=\{app\}\\\{#ProductExe\}/);
});
test("Step27: offline WebView2 x64 is bundled; no Python or Java end-user prerequisite",()=>{
 assert.match(setup,/MicrosoftEdgeWebView2RuntimeInstallerX64\.exe/);
 assert.match(setup,/IsWebView2Installed/);
 assert.match(setup,/waituntilterminated/);
 assert.match(setup,/Parameters: "\/silent \/install"/);
 assert.match(build,/Get-AuthenticodeSignature/);
 assert.match(build,/Status -ne "Valid"/);
 assert.match(build,/SignerCertificate\.Subject -notmatch "Microsoft"/);
 assert.match(build,/100MB/);
 assert.match(build,/Get-FileHash -Algorithm SHA256/);
 assert.doesNotMatch(setup,/python\.exe|java\.exe|jdk|signal-cli/i);
});
test("Step27: data isolation, backup preservation and explicit purge only",()=>{
 assert.match(cs,/Path\.Combine\(canonical,Company,Product\)/);
 assert.match(cs,/AssertNoInstallDirectoryOverlap/);
 assert.match(desktop,/CoreWebView2Environment\.CreateAsync/);
 assert.match(desktop,/UserDataPaths\.WebView2\(dataRoot\)/);
 assert.doesNotMatch(setup,/\[UninstallDelete\]/i);
 assert.doesNotMatch(setup,/\[InstallDelete\]/i);
 assert.doesNotMatch(setup,/DelTree\(|RemoveDir\(/i);
 assert.match(cleanup,/if\(!\$Purge\)/);
 assert.match(cleanup,/\$Confirmation -cne/);
 assert.match(cleanup,/ReparsePoint/);
 assert.match(cleanup,/Remove-Item -LiteralPath \$root -Recurse -Force/);
});
