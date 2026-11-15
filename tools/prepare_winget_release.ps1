#Requires -Version 5.1
<#
.SYNOPSIS
  Hash a QuantumUpdater.exe build and write winget installer SHA256 + URL.

.EXAMPLE
  .\tools\prepare_winget_release.ps1 -ExePath .\dist\QuantumUpdater\QuantumUpdater.exe -Version 2.0.1
#>
param(
  [Parameter(Mandatory = $true)]
  [string]$ExePath,
  [Parameter(Mandatory = $true)]
  [string]$Version,
  [string]$Repo = "merchanthasan2/AudoChocolatey",
  [string]$AssetName = "QuantumUpdater.exe",
  [string]$ManifestRoot = ""
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path -LiteralPath $ExePath)) {
  throw "EXE not found: $ExePath"
}

$hash = (Get-FileHash -LiteralPath $ExePath -Algorithm SHA256).Hash.ToUpperInvariant()
$tag = if ($Version.StartsWith("v")) { $Version } else { "v$Version" }
$ver = $Version.TrimStart("v")
$url = "https://github.com/$Repo/releases/download/$tag/$AssetName"

if (-not $ManifestRoot) {
  $ManifestRoot = Join-Path $PSScriptRoot "..\manifests\w\QuantumStep\QuantumUpdater\$ver"
}
$ManifestRoot = [System.IO.Path]::GetFullPath($ManifestRoot)
New-Item -ItemType Directory -Force -Path $ManifestRoot | Out-Null

$installerPath = Join-Path $ManifestRoot "QuantumStep.QuantumUpdater.installer.yaml"
$versionPath = Join-Path $ManifestRoot "QuantumStep.QuantumUpdater.yaml"
$localePath = Join-Path $ManifestRoot "QuantumStep.QuantumUpdater.locale.en-US.yaml"
$templateLocale = Join-Path $PSScriptRoot "..\manifests\w\QuantumStep\QuantumUpdater\2.0.0\QuantumStep.QuantumUpdater.locale.en-US.yaml"

@"
PackageIdentifier: QuantumStep.QuantumUpdater
PackageVersion: $ver
MinimumOSVersion: 10.0.17763.0
InstallerType: portable
Scope: user
UpgradeBehavior: install
Commands:
  - QuantumUpdater
Installers:
  - Architecture: x64
    InstallerUrl: $url
    InstallerSha256: $hash
    InstallerLocale: en-US
ManifestType: installer
ManifestVersion: 1.6.0
"@ | Set-Content -LiteralPath $installerPath -Encoding utf8

@"
PackageIdentifier: QuantumStep.QuantumUpdater
PackageVersion: $ver
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.6.0
"@ | Set-Content -LiteralPath $versionPath -Encoding utf8

if (Test-Path -LiteralPath $templateLocale) {
  $locale = Get-Content -LiteralPath $templateLocale -Raw
  $locale = $locale -replace "PackageVersion:\s*[\d\.]+", "PackageVersion: $ver"
  Set-Content -LiteralPath $localePath -Value $locale -Encoding utf8
}

Write-Host "Winget manifests ready under $ManifestRoot"
Write-Host "InstallerUrl: $url"
Write-Host "InstallerSha256: $hash"
Write-Host ""
Write-Host "After the GitHub Release exists with that asset, submit with:"
Write-Host "  wingetcreate submit $ManifestRoot"
Write-Host "or open a PR under microsoft/winget-pkgs manifests/q/QuantumStep/QuantumUpdater/$ver/"

@{
  Version = $ver
  Tag = $tag
  Url = $url
  Sha256 = $hash
  ManifestRoot = $ManifestRoot
}
