; MOSEN Telegram Scheduler, independent from Signal MS002
#define ProductName "MOSEN Telegram Scheduler"
#define ProductExe "Mosen.GroupScheduler.Desktop.exe"
#ifndef ProductVersion
  #define ProductVersion "0.29.2"
#endif
#ifndef OutputTag
  #define OutputTag "0.29.2-dev"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\desktop"
#endif
#ifndef RuntimeFile
  #define RuntimeFile "..\build\vendor\MicrosoftEdgeWebView2RuntimeInstallerX64.exe"
#endif
#ifndef BrandingFile
  #define BrandingFile "..\assets\branding\generated\app.ico"
#endif
#ifndef InstallerOutput
  #define InstallerOutput "..\artifacts\installer"
#endif
[Setup]
AppId={{1DD1DE0A-6F59-4D52-A25E-78CB07A25701}
AppName={#ProductName}
AppVersion={#ProductVersion}
AppPublisher=MOSEN
AppPublisherURL=https://github.com/mosen6266-netizen/MS_001
DefaultDirName={localappdata}\Programs\MOSEN\TelegramScheduler
DefaultGroupName={#ProductName}
DisableDirPage=yes
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
Uninstallable=yes
UninstallDisplayName={#ProductName}
UninstallDisplayIcon={app}\{#ProductExe}
OutputDir={#InstallerOutput}
OutputBaseFilename=MOSEN-Telegram-Scheduler-Setup-{#OutputTag}
SetupIconFile={#BrandingFile}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
AppMutex=Local\MOSEN.GroupScheduler.Desktop.Singleton
CloseApplications=yes
RestartApplications=no
ChangesEnvironment=no
ChangesAssociations=no
SetupLogging=yes
; NEVER delete {localappdata}\MOSEN\TelegramScheduler by default or after upgrading.
; Developer CI package is unsigned. Production Release workflow must sign both installed EXE and Setup.

[Languages]
Name: "chinesesimp"; MessagesFile: "lang\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "在桌面创建快捷方式"; GroupDescription: "快捷方式"; Flags: checkedonce

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RuntimeFile}"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "..\build\vendor\VC_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "..\build\Remove-LocalUserData.ps1"; DestDir: "{app}\tools"; Flags: ignoreversion
Source: "..\updater\Apply-Update.ps1"; DestDir: "{app}\tools"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#ProductName}"; Filename: "{app}\{#ProductExe}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#ProductName}"; Filename: "{app}\{#ProductExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{tmp}\VC_redist.x64.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "配置 Telegram 原生运行所需的 Microsoft VC++ 环境"; Flags: waituntilterminated; Check: not IsVCRuntimeInstalled
Filename: "{tmp}\MicrosoftEdgeWebView2RuntimeInstallerX64.exe"; Parameters: "/silent /install"; StatusMsg: "配置 WebView2 离线运行环境"; Flags: waituntilterminated; Check: not IsWebView2Installed
Filename: "{app}\{#ProductExe}"; Description: "启动 MOSEN Telegram Scheduler"; Flags: nowait postinstall skipifsilent; Check: IsWebView2Installed

[Code]
function IsVCRuntimeInstalled: Boolean;
var
  Installed: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Installed', Installed)
    and (Installed = 1);
end;

function IsWebView2Installed: Boolean;
var
  Version: string;
begin
  Result := False;
  if RegQueryStringValue(HKCU, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) then
    Result := (Version <> '') and (Version <> '0.0.0.0');
  if (not Result) and RegQueryStringValue(HKLM32, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) then
    Result := (Version <> '') and (Version <> '0.0.0.0');
end;
