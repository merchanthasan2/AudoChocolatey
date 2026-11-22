# Invokes DeskVerve / QuantumUpdater scheduled cleanup (no UI).
$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot '..\DeskVerve.exe'
if (-not (Test-Path -LiteralPath $exe)) {
  $exe = Join-Path $PSScriptRoot '..\QuantumUpdater.exe'
}
if (Test-Path -LiteralPath $exe) {
  & $exe --scheduled-cleanup --no-elevate
  exit $LASTEXITCODE
}
$main = Join-Path $PSScriptRoot '..\main.py'
if (Test-Path -LiteralPath $main) {
  python $main --scheduled-cleanup --no-elevate
  exit $LASTEXITCODE
}
Write-Error 'QuantumUpdater executable not found.'
exit 1