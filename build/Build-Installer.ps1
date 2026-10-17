# Run on Windows build runner. Produce an unsigned Step27 development installer.
# Final signing, secure update and rollback are reserved for Step28.
[CmdletBinding()]
param(
 [Parameter(Mandatory=$false)][string]$Published = "artifacts/desktop",
 [Parameter(Mandatory=$false)][string]$Output = "artifacts/installer",
 [Parameter(Mandatory=$false)][string]$InnoCompiler = "",
 [Parameter(Mandatory=$false)][string]$PackageVersion = "0.29.0",
 [Parameter(Mandatory=$false)][string]$PackageFileTag = "0.29.0-dev"
)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
function ResolveRepo([string]$p) {
 if([IO.Path]::IsPathRooted($p)){return [IO.Path]::GetFullPath($p)}
 return [IO.Path]::GetFullPath((Join-Path $repo $p))
}
$publishDir=ResolveRepo $Published
$outputDir=ResolveRepo $Output
$icon=Join-Path $repo "assets/branding/generated/app.ico"
$runtime=Join-Path $repo "build/vendor/MicrosoftEdgeWebView2RuntimeInstallerX64.exe"
$exe=Join-Path $publishDir "Mosen.GroupScheduler.Desktop.exe"
$ui=Join-Path $publishDir "ui/index.html"
foreach($file in @($exe,$ui,$icon,$runtime)){
 if(!(Test-Path -LiteralPath $file -PathType Leaf)){throw "Missing required installer input file: $file"}
}
$signature=Get-AuthenticodeSignature -LiteralPath $runtime
if($signature.Status -ne "Valid" -or $signature.SignerCertificate.Subject -notmatch "Microsoft"){
 throw "Offline WebView2 Runtime not Authenticode-signed by Microsoft; refuse release packaging."
}
$compiler=$InnoCompiler
if([string]::IsNullOrWhiteSpace($compiler)){
 $locations=@(
  "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
  "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
 )
 $compiler=$locations|Where-Object{Test-Path -LiteralPath $_ -PathType Leaf}|Select-Object -First 1
}
if([string]::IsNullOrWhiteSpace($compiler) -or !(Test-Path -LiteralPath $compiler -PathType Leaf)){
 throw "Inno Setup 6 compiler not found. Install on build runner, not user computer."
}
New-Item -ItemType Directory -Force -Path $outputDir|Out-Null
$script=Join-Path $repo "installer/Setup.iss"
& $compiler "/DPublishDir=$publishDir" "/DRuntimeFile=$runtime" "/DBrandingFile=$icon" "/DInstallerOutput=$outputDir" "/DProductVersion=$PackageVersion" "/DOutputTag=$PackageFileTag" $script
if($LASTEXITCODE -ne 0){throw "Inno Setup compiler failed: $LASTEXITCODE"}
$package=Join-Path $outputDir "MOSEN-Telegram-Scheduler-Setup-$PackageFileTag.exe"
if(!(Test-Path -LiteralPath $package -PathType Leaf)){throw "Expected installer was not created."}
$bytes=(Get-Item -LiteralPath $package).Length
if($bytes -lt 100MB){throw "Installer too small for bundled offline WebView2 Runtime: $bytes bytes."}
$hash=(Get-FileHash -Algorithm SHA256 -LiteralPath $package).Hash
Set-Content -LiteralPath "$package.sha256" -Value "$hash  $(Split-Path -Leaf $package)" -Encoding ascii
Write-Host "Step27 generated unsigned offline installer: $(Split-Path -Leaf $package)"
Write-Host "Bytes: $bytes | SHA256: $hash"
