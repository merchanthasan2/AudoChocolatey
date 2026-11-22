#Requires -Version 5.1
<#
.SYNOPSIS
  Hash a DeskVerve installer / portable build and write winget manifests (1.10.0 schema).

.EXAMPLE
  .\tools\prepare_winget_release.ps1 -ExePath .\dist\release\DeskVerve-Setup-3.0.0-x64.exe -Version 3.0.0
#>
param(
  [Parameter(Mandatory = $true)]
  [string]$ExePath,
  [string]$Version = "3.0.0",
  [string]$Repo = "merchanthasan2/AudoChocolatey",
  [string]$AssetName = "",
  [string]$ManifestRoot = ""
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path -LiteralPath $ExePath)) {
  throw "Installer not found: $ExePath"
}

function Get-Sha256Hex([string]$filePath) {
  $sha = [System.Security.Cryptography.SHA256]::Create()
  $stream = [System.IO.File]::OpenRead($filePath)
  try {
    $bytes = $sha.ComputeHash($stream)
    return [System.BitConverter]::ToString($bytes).Replace("-", "").ToUpperInvariant()
  }
  finally {
    $stream.Dispose()
    $sha.Dispose()
  }
}

$hash = Get-Sha256Hex -filePath $ExePath
$tag = if ($Version.StartsWith("v")) { $Version } else { "v$Version" }
$ver = $Version.TrimStart("v")
if (-not $PSBoundParameters.ContainsKey("AssetName") -or [string]::IsNullOrWhiteSpace($AssetName)) {
  $AssetName = [System.IO.Path]::GetFileName($ExePath)
}
$url = "https://github.com/$Repo/releases/download/$tag/$AssetName"

# winget-pkgs partition: lowercase first letter of publisher ('q' for QuantumStep)
if (-not $ManifestRoot) {
  $ManifestRoot = Join-Path $PSScriptRoot "..\manifests\q\QuantumStep\QuantumUpdater\$ver"
}
$ManifestRoot = [System.IO.Path]::GetFullPath($ManifestRoot)
New-Item -ItemType Directory -Force -Path $ManifestRoot | Out-Null

$installerPath = Join-Path $ManifestRoot "QuantumStep.QuantumUpdater.installer.yaml"
$versionPath = Join-Path $ManifestRoot "QuantumStep.QuantumUpdater.yaml"
$localePath = Join-Path $ManifestRoot "QuantumStep.QuantumUpdater.locale.en-US.yaml"

@"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.1.10.0.schema.json
PackageIdentifier: QuantumStep.QuantumUpdater
PackageVersion: $ver
MinimumOSVersion: 10.0.17763.0
InstallerType: inno
Scope: machine
InstallModes:
  - interactive
  - silent
  - silentWithProgress
UpgradeBehavior: install
Commands:
  - DeskVerve
Installers:
  - Architecture: x64
    InstallerUrl: $url
    InstallerSha256: $hash
ManifestType: installer
ManifestVersion: 1.10.0
"@ | Set-Content -LiteralPath $installerPath -Encoding utf8

@"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.1.10.0.schema.json
PackageIdentifier: QuantumStep.QuantumUpdater
PackageVersion: $ver
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.10.0
"@ | Set-Content -LiteralPath $versionPath -Encoding utf8

@"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.1.10.0.schema.json
PackageIdentifier: QuantumStep.QuantumUpdater
PackageVersion: $ver
PackageLocale: en-US
Publisher: Quantum Step
PublisherUrl: https://github.com/merchanthasan2/AudoChocolatey
PublisherSupportUrl: https://github.com/merchanthasan2/AudoChocolatey/issues
Author: Quantum Step
PackageName: DeskVerve
PackageUrl: https://github.com/merchanthasan2/AudoChocolatey
License: MIT
LicenseUrl: https://raw.githubusercontent.com/merchanthasan2/AudoChocolatey/channel/windows/LICENSE
Copyright: Copyright (c) 2026 Quantum Step
ShortDescription: DeskVerve keeps your Windows apps up to date.
Description: |-
  DeskVerve is a Windows desktop app from Quantum Step that checks,
  updates, and installs software using winget. Requires administrator on launch.
Moniker: deskverve
Tags:
  - quantum-step
  - deskverve
  - updater
  - package-manager
  - winget
  - software-manager
  - windows-tools
ReleaseNotesUrl: https://github.com/merchanthasan2/AudoChocolatey/releases/tag/$tag
ManifestType: defaultLocale
ManifestVersion: 1.10.0
"@ | Set-Content -LiteralPath $localePath -Encoding utf8

Write-Host "Winget manifests ready under $ManifestRoot"
Write-Host "InstallerUrl: $url"
Write-Host "InstallerSha256: $hash"
Write-Host ""
Write-Host "Validate with:"
Write-Host "  winget validate `"$ManifestRoot`""
Write-Host "After the GitHub Release exists with that asset, submit with:"
Write-Host "  wingetcreate submit `"$ManifestRoot`""
Write-Host "or open a PR under microsoft/winget-pkgs manifests/q/QuantumStep/QuantumUpdater/$ver/"

[PSCustomObject]@{
  Version      = $ver
  Tag          = $tag
  Url          = $url
  Sha256       = $hash
  ManifestRoot = $ManifestRoot
}
