# Windows runner wrapper: all PKCS8 handling, FixedTimeEquals and SignData execute in .NET10.
[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+$')][string]$Version,
 [Parameter(Mandatory=$true)][string]$PackagePath,
 [Parameter(Mandatory=$true)][string]$Destination,
 [string]$MinimumVersion = "0.27.0"
)
$ErrorActionPreference="Stop"
$repo=(Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Push-Location $repo
try {
 dotnet run --project "updater/ReleaseSigner/Mosen.GroupScheduler.ReleaseSigner.csproj" --configuration Release -- $Version $PackagePath $Destination $MinimumVersion
 if($LASTEXITCODE -ne 0){throw "Standalone signed update.json generation failed"}
}finally{Pop-Location}
