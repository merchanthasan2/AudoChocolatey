#Requires -Version 5.1
<#
  Phase A cutover will rename/replace QuantumUpdater-Windows.zip after owner accept.
  Until then this script writes py-artifacts/QuantumUpdater-Windows-WinUI.zip only.
#>
$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$PublishDir = Join-Path $RepoRoot "dist\QuantumUpdater-WinUI"
$ZipPath = Join-Path $RepoRoot "py-artifacts\QuantumUpdater-Windows-WinUI.zip"
$Csproj = Join-Path $RepoRoot "src\QuantumUpdater\QuantumUpdater.csproj"

$running = Get-Process -Name "DeskVerve", "QuantumUpdater" -ErrorAction SilentlyContinue
if ($running) {
  Write-Host "Stopping running process(es)..."
  $running | Stop-Process -Force
  Start-Sleep -Seconds 2
}

Push-Location $RepoRoot
try {
  dotnet publish $Csproj -c Release -r win-x64 --self-contained true -o $PublishDir
  if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
  }
}
finally {
  Pop-Location
}

$exePath = Join-Path $PublishDir "DeskVerve.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
  $exePath = Join-Path $PublishDir "QuantumUpdater.exe"
}
if (-not (Test-Path -LiteralPath $exePath)) {
  throw "EXE not found after publish: $exePath"
}

$pyArtifacts = Join-Path $RepoRoot "py-artifacts"
New-Item -ItemType Directory -Force -Path $pyArtifacts | Out-Null
if (Test-Path -LiteralPath $ZipPath) {
  Remove-Item -LiteralPath $ZipPath -Force
}
Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $ZipPath -Force

$zipItem = Get-Item -LiteralPath $ZipPath
$sha256 = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash.ToUpperInvariant()

Write-Host ""
Write-Host "Publish OK"
Write-Host "EXE: $exePath"
Write-Host "Zip: $($zipItem.FullName)"
Write-Host "Size: $($zipItem.Length) bytes"
Write-Host "SHA256: $sha256"