# Optional explicit user-data destruction only after uninstall and separate backup.
# Default uninstall from Windows Settings NEVER calls this script.
[CmdletBinding()]
param([switch]$Purge,[string]$Confirmation="")
$ErrorActionPreference="Stop"
Set-StrictMode -Version Latest
$local=[Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
if([string]::IsNullOrWhiteSpace($local)){throw "LocalAppData unavailable"}
$root=[IO.Path]::GetFullPath((Join-Path $local "MOSEN/TelegramScheduler")).TrimEnd("\","/")
$expected=[IO.Path]::GetFullPath((Join-Path $local "MOSEN/TelegramScheduler")).TrimEnd("\","/")
if($root -ne $expected -or $root.Length -lt 20){throw "Unexpected user profile path"}
Write-Host "User data: $root"
if(!$Purge){
 Write-Host "Default: KEEP user data. To deliberately destroy all data after uninstall, run this script with -Purge -Confirmation '删除Telegram本地数据'."
 exit 0
}
if($Confirmation -cne "删除Telegram本地数据"){throw "Explicit exact confirmation required."}
if(Get-Process -Name "Mosen.GroupScheduler.Desktop" -ErrorAction SilentlyContinue){
 throw "Close MOSEN Telegram Scheduler before erasing its data."
}
if(!(Test-Path -LiteralPath $root)){Write-Host "No Telegram data to delete.";exit 0}
$profile=(Get-Item -LiteralPath $local -Force)
if(($profile.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw "Refuse redirected user profile."}
$company=(Get-Item -LiteralPath (Join-Path $local "MOSEN") -Force)
if(($company.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw "Refuse redirected application parent."}
$item=Get-Item -LiteralPath $root -Force
if(($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw "Refuse redirected application root."}
$entries=@(Get-ChildItem -LiteralPath $root -Force -Recurse -ErrorAction Stop)
if(@($entries|Where-Object{($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0}).Count -ne 0){
 throw "Refuse to remove data containing junctions or reparse-point files."
}
Write-Warning "PERMANENTLY deleting TelegramScheduler local data, sessions, scripts, logs and backups."
Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction Stop
Write-Host "Application user data deleted following explicit consent."
