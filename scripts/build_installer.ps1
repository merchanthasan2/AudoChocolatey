#Requires -Version 5.1
<#
.SYNOPSIS
  Publishes DeskVerve WinUI (self-contained, Release, win-x64),
  builds DeskVerve-Windows.zip, compiles Inno Setup (DeskVerve-Setup-<version>-x64.exe),
  and writes SHA256 checksums to dist\release\.
#>
[CmdletBinding()]
param(
  [string]$Configuration = "Release",
  [string]$Runtime = "win-x64",
  [string]$Version = ""
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$Csproj = Join-Path $RepoRoot "src\QuantumUpdater\QuantumUpdater.csproj"
$PublishDir = Join-Path $RepoRoot "dist\QuantumUpdater-WinUI"
$ReleaseDir = Join-Path $RepoRoot "dist\release"
$IssFile = Join-Path $RepoRoot "installer\DeskVerve.iss"

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

# 1. Determine Version
if ([string]::IsNullOrWhiteSpace($Version)) {
  $xml = [xml](Get-Content -LiteralPath $Csproj)
  $Version = $xml.Project.PropertyGroup.Version | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1
  if (-not $Version) {
    $Version = "3.0.0"
  }
}
$ver = $Version.TrimStart("v")
Write-Host "Building DeskVerve version: $ver ($Configuration, $Runtime)"

# 2. Stop running processes
$running = Get-Process -Name "DeskVerve", "QuantumUpdater" -ErrorAction SilentlyContinue
if ($running) {
  Write-Host "Stopping running process(es)..."
  $running | Stop-Process -Force
  Start-Sleep -Seconds 2
}

# 3. Publish self-contained WinUI
Write-Host "Publishing self-contained WinUI app..."
New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null

Push-Location $RepoRoot
try {
  dotnet publish $Csproj -c $Configuration -r $Runtime --self-contained true -o $PublishDir
  if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
  }
}
finally {
  Pop-Location
}

$exePath = Join-Path $PublishDir "DeskVerve.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
  throw "DeskVerve.exe not found after publish: $exePath"
}

# 4. Prepare Release output directory
New-Item -ItemType Directory -Force -Path $ReleaseDir | Out-Null

# 5. Create DeskVerve-Windows.zip (portable)
$zipPath = Join-Path $ReleaseDir "DeskVerve-Windows.zip"
Write-Host "Creating portable archive: $zipPath..."
if (Test-Path -LiteralPath $zipPath) {
  Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $zipPath -Force
$zipHash = Get-Sha256Hex -filePath $zipPath
Set-Content -LiteralPath "$zipPath.sha256" -Value $zipHash -Encoding ascii

# Also keep py-artifacts/QuantumUpdater-Windows-WinUI.zip for legacy scripts if needed
$legacyArtifacts = Join-Path $RepoRoot "py-artifacts"
New-Item -ItemType Directory -Force -Path $legacyArtifacts | Out-Null
Copy-Item -LiteralPath $zipPath -Destination (Join-Path $legacyArtifacts "QuantumUpdater-Windows-WinUI.zip") -Force

# 6. Locate Inno Setup compiler (ISCC.exe)
$isccCmd = Get-Command iscc -ErrorAction SilentlyContinue
$iscc = if ($isccCmd) { $isccCmd.Source } else { $null }
if (-not $iscc) {
  $candidates = @(
    "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
  )
  foreach ($cand in $candidates) {
    if (Test-Path -LiteralPath $cand) {
      $iscc = $cand
      break
    }
  }
}
if (-not $iscc) {
  throw "Inno Setup compiler (ISCC.exe) not found. Install Inno Setup 6 via 'winget install --id JRSoftware.InnoSetup -e' or 'choco install innosetup -y'."
}
Write-Host "Found Inno Setup compiler: $iscc"

# 7. Compile Inno Setup installer
Write-Host "Compiling setup installer with Inno Setup..."
$setupBaseName = "DeskVerve-Setup-$ver-x64"
$expectedSetupExe = Join-Path $ReleaseDir "$setupBaseName.exe"
if (Test-Path -LiteralPath $expectedSetupExe) {
  Remove-Item -LiteralPath $expectedSetupExe -Force
}

& $iscc "/DMyAppVersion=$ver" "/DSourceDir=$PublishDir" "/DOutputDir=$ReleaseDir" "/F$setupBaseName" "$IssFile"
if ($LASTEXITCODE -ne 0) {
  throw "ISCC compilation failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $expectedSetupExe)) {
  throw "Setup EXE not found after compilation: $expectedSetupExe"
}

$setupHash = Get-Sha256Hex -filePath $expectedSetupExe
Set-Content -LiteralPath "$expectedSetupExe.sha256" -Value $setupHash -Encoding ascii

$zipItem = Get-Item -LiteralPath $zipPath
$setupItem = Get-Item -LiteralPath $expectedSetupExe

Write-Host ""
Write-Host "=== Build Complete ==="
Write-Host "Portable Zip : $($zipItem.FullName) ($($zipItem.Length) bytes)"
Write-Host "Zip SHA256   : $zipHash"
Write-Host "Setup EXE    : $($setupItem.FullName) ($($setupItem.Length) bytes)"
Write-Host "Setup SHA256 : $setupHash"
Write-Host "======================"

[PSCustomObject]@{
  Version     = $ver
  ZipPath     = $zipItem.FullName
  ZipSha256   = $zipHash
  SetupPath   = $setupItem.FullName
  SetupSha256 = $setupHash
}
