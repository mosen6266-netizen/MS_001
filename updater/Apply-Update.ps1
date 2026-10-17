# Stage 2: run as a separate Windows PowerShell 5.1 process after GUI exit.
[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$Installer,
 [Parameter(Mandatory=$true)][ValidatePattern('^[a-fA-F0-9]{64}$')][string]$ExpectedSHA256,
 [Parameter(Mandatory=$true)][ValidatePattern('^[a-fA-F0-9]{40}$')][string]$PublisherThumbprint,
 [Parameter(Mandatory=$true)][int]$ParentProcessId
)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$privateData=[IO.Path]::GetFullPath([IO.Path]::Combine($env:LOCALAPPDATA,"MOSEN","TelegramScheduler"))
$program=[IO.Path]::GetFullPath([IO.Path]::Combine($env:LOCALAPPDATA,"Programs","MOSEN","TelegramScheduler"))
$staging=[IO.Path]::GetFullPath([IO.Path]::Combine($env:LOCALAPPDATA,"MOSEN","TelegramSchedulerUpdates"))
$installerFull=[IO.Path]::GetFullPath($Installer)
if(!$installerFull.StartsWith($staging+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw "Installer is outside trusted staging folder"}
if($privateData.StartsWith($program+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or $program.StartsWith($privateData+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw "Private data and installation paths overlap"}
foreach($p in @($staging,$program,$installerFull)){
 if(!(Test-Path -LiteralPath $p)){throw "Missing update input or installed app"}
 if(((Get-Item -LiteralPath $p -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw "Reparse point blocked"}
}
function CheckPackage {
 $sha=(Get-FileHash -Algorithm SHA256 -LiteralPath $installerFull).Hash
 if($sha -cne $ExpectedSHA256.ToUpperInvariant()){throw "SHA256 mismatch"}
 $signed=Get-AuthenticodeSignature -LiteralPath $installerFull
 if($signed.Status -ne "Valid" -or $null -eq $signed.SignerCertificate -or
  $signed.SignerCertificate.Thumbprint.ToUpperInvariant() -cne $PublisherThumbprint.ToUpperInvariant()){
    throw "Publisher Authenticode signature invalid or certificate thumbprint changed"
 }
}
CheckPackage
if($ParentProcessId -gt 0) {
 try {Wait-Process -Id $ParentProcessId -Timeout 90 -ErrorAction Stop}
 catch {if(Get-Process -Id $ParentProcessId -ErrorAction SilentlyContinue){throw "Please close the app before installing"}}
}
if(Get-Process -Name "Mosen.GroupScheduler.Desktop" -ErrorAction SilentlyContinue) {throw "Running app blocks update"}
$backup=Join-Path $staging "previous-program"
$tempBackup=Join-Path $staging "previous-program.pending"
if(Test-Path -LiteralPath $backup){throw "Existing recovery backup: resolve first"}
if(Test-Path -LiteralPath $tempBackup){throw "Interrupted backup: manual recovery required"}
if(!(Test-Path -LiteralPath (Join-Path $program "Mosen.GroupScheduler.Desktop.exe"))){throw "Installed exe missing"}
# Reject links inside installation: do not follow external targets while cloning.
Get-ChildItem -LiteralPath $program -Force -Recurse | ForEach-Object {
 if(($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw "Linked installed files are not supported for safe rollback"}
}
Copy-Item -LiteralPath $program -Destination $tempBackup -Recurse -Force
if(!(Test-Path -LiteralPath (Join-Path $tempBackup "Mosen.GroupScheduler.Desktop.exe"))){throw "Backup incomplete"}
Move-Item -LiteralPath $tempBackup -Destination $backup
CheckPackage
$failure=""
try {
 $process=Start-Process -FilePath $installerFull -ArgumentList "/VERYSILENT","/SUPPRESSMSGBOXES","/NORESTART" -PassThru -Wait
 if($process.ExitCode -ne 0){throw "Installer returned $($process.ExitCode)"}
 if(!(Test-Path -LiteralPath (Join-Path $program "Mosen.GroupScheduler.Desktop.exe"))){throw "Installed app missing after update"}
 Write-Host "Install completed; previous program backup retained at $backup"
 exit 0
}catch{$failure=$_.Exception.Message}
# Never touch PrivateData, SQLite, TDLib sessions, backups, or browser profile.
$restored=$false
try{
 if(Test-Path -LiteralPath $program){Remove-Item -LiteralPath $program -Recurse -Force}
 Copy-Item -LiteralPath $backup -Destination $program -Recurse -Force
 $restored=(Test-Path -LiteralPath (Join-Path $program "Mosen.GroupScheduler.Desktop.exe"))
}catch{$restored=$false}
if($restored){throw "Upgrade failed ($failure). Previous program files restored; private user data untouched."}
throw "Upgrade failed ($failure). Automatic restoration incomplete; recover program files from $backup. Do not delete private data."
