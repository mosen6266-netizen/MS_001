# Trusted Windows release runner. Never commit the PKCS8 private key.
[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+$')][string]$Version,
 [Parameter(Mandatory=$true)][string]$PackagePath,
 [Parameter(Mandatory=$true)][string]$Destination,
 [string]$MinimumVersion = "0.27.0"
)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$private = $env:UPDATE_SIGNING_PKCS8_BASE64
if ([string]::IsNullOrWhiteSpace($private)) { throw "Missing update signing secret" }
$pin = (Get-Content -Raw -LiteralPath (Join-Path $repo "updater/trust/release-v1.spki.base64")).Trim()
if ($pin -eq "UNCONFIGURED" -or [string]::IsNullOrWhiteSpace($pin)) { throw "Publisher key UNCONFIGURED" }
$spki = [Convert]::FromBase64String($pin)
$key = [Security.Cryptography.ECDsa]::Create()
try {
 $read=0
 $key.ImportPkcs8PrivateKey([Convert]::FromBase64String($private),[ref]$read)
 $export=$key.ExportSubjectPublicKeyInfo()
 if(-not [Security.Cryptography.CryptographicOperations]::FixedTimeEquals($spki,$export)) { throw "Mismatched public/private key" }
 $file="MOSEN-Telegram-Scheduler-Setup-$Version.exe"
 if ((Split-Path -Leaf $PackagePath) -cne $file) { throw "Wrong release filename" }
 $info=Get-Item -LiteralPath $PackagePath
 if ($info.Length -lt 100MB -or $info.Length -gt 768MB) { throw "Invalid package size" }
 $sha=(Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash
 $payload=[ordered]@{
   product="MOSEN_TELEGRAM_SCHEDULER"
   version=$Version
   minimumVersion=$MinimumVersion
   fileName=$file
   downloadUrl="https://github.com/mosen6266-netizen/MS_001/releases/download/v$Version/$file"
   size=[long]$info.Length
   sha256=$sha
 }
 $bytes=[Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject $payload -Compress -Depth 4))
 $signature=$key.SignData($bytes,[Security.Cryptography.HashAlgorithmName]::SHA256,
     [Security.Cryptography.DSASignatureFormat]::IeeeP1363FixedFieldConcatenation)
 $envelope=[ordered]@{
   schema=1
   keyId="release-v1"
   payload=[Convert]::ToBase64String($bytes)
   signature=[Convert]::ToBase64String($signature)
 }
 $json=ConvertTo-Json -InputObject $envelope -Compress -Depth 4
 [IO.File]::WriteAllText([IO.Path]::GetFullPath($Destination),$json,[Text.UTF8Encoding]::new($false))
 Write-Host "Signed manifest for $file, SHA256 $sha"
}finally{$key.Dispose()}
