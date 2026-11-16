# TEMP DIAGNOSTIC 46.7.36. Starts an app many times and records how each start ends.
# Remove with task 46.7.85 when the start crash is fixed.
# Example: powershell -NoProfile -ExecutionPolicy Bypass -File tools\diag\start-test.ps1 -Exe "C:\path\QuantumUpdater.exe" -Count 10 -Variant V0
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [int]$Count = 10,
    [string]$Diag = "",
    [string]$Variant = "V0",
    [int]$WaitSeconds = 6,
    [switch]$Matrix
)

$ErrorActionPreference = 'Stop'

$exePath = (Resolve-Path -LiteralPath $Exe).ProviderPath
$exeName = [System.IO.Path]::GetFileNameWithoutExtension($exePath)
$exeFolder = [System.IO.Path]::GetDirectoryName($exePath)
$appFolder = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'QuantumUpdater'
$crashLog = Join-Path $appFolder 'winui-crash.log'
$logFolder = Join-Path $appFolder 'logs'
$startLogFolder = Join-Path $logFolder 'start-tests'
$csvPath = Join-Path $logFolder 'start-test.csv'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$invariant = [System.Globalization.CultureInfo]::InvariantCulture

function Quote-Csv([string]$text) {
    return '"' + ($text -replace '"', '""') + '"'
}

function Add-CsvLine([string[]]$fields) {
    if (-not (Test-Path -LiteralPath $csvPath)) {
        $header = @('time', 'variant', 'diag', 'n', 'result', 'last_mark', 'seconds_to_exit', 'note')
        [System.IO.File]::AppendAllText($csvPath, (($header | ForEach-Object { Quote-Csv $_ }) -join ',') + "`r`n", $utf8)
    }
    $line = ($fields | ForEach-Object { Quote-Csv $_ }) -join ','
    [System.IO.File]::AppendAllText($csvPath, $line + "`r`n", $utf8)
}

function Get-LastMark([string]$path) {
    $mark = ''
    foreach ($line in [System.IO.File]::ReadAllLines($path)) {
        if ($line -match '^\S+ \[(Mark:.*)\]') {
            $mark = $matches[1]
        }
    }
    return $mark
}

function Stop-OldCopies([string]$name) {
    $running = @(Get-Process -Name $name -ErrorAction SilentlyContinue)
    if ($running.Count -eq 0) {
        return ''
    }

    $ids = ($running | ForEach-Object { $_.Id }) -join ' '
    foreach ($old in $running) {
        Stop-Process -Id $old.Id -Force -ErrorAction SilentlyContinue
    }
    foreach ($old in $running) {
        [void]$old.WaitForExit(10000)
    }
    return "stopped old copy (pid $ids) before start"
}

function Invoke-Start([string]$variantName, [string]$diagText, [int]$n) {
    $note = Stop-OldCopies $exeName

    if (Test-Path -LiteralPath $crashLog) {
        Remove-Item -LiteralPath $crashLog -Force
    }

    $env:DESKVERVE_DIAG = $diagText
    $started = Get-Date
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process -FilePath $exePath -WorkingDirectory $exeFolder -PassThru
    $exited = $p.WaitForExit($WaitSeconds * 1000)

    if ($exited) {
        $result = '0x{0:X8}' -f ([int64]$p.ExitCode -band [int64]4294967295)
        $seconds = [Math]::Round($watch.Elapsed.TotalSeconds, 1).ToString('0.0', $invariant)
    }
    else {
        $result = 'alive'
        $seconds = ''
        Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
        [void]$p.WaitForExit(10000)
    }

    $mark = ''
    if (Test-Path -LiteralPath $crashLog) {
        $copyPath = Join-Path $startLogFolder ("start-{0}-{1}.log" -f $variantName, $n)
        Copy-Item -LiteralPath $crashLog -Destination $copyPath -Force
        $mark = Get-LastMark $copyPath
    }

    Add-CsvLine @($started.ToString('s'), $variantName, $diagText, [string]$n, $result, $mark, $seconds, $note)

    return [pscustomobject]@{ Result = $result; Mark = $mark }
}

New-Item -ItemType Directory -Force -Path $startLogFolder | Out-Null

if ($Matrix) {
    $variants = @(
        @{ Name = 'V0'; Diag = '' },
        @{ Name = 'V1'; Diag = 'nomica' },
        @{ Name = 'V2'; Diag = 'notitlebar' },
        @{ Name = 'V3'; Diag = 'titlebarlate' },
        @{ Name = 'V4'; Diag = 'noemblem' },
        @{ Name = 'V5'; Diag = 'noscan' },
        @{ Name = 'V6'; Diag = 'nofirstchance' },
        @{ Name = 'V7'; Diag = 'nomica,notitlebar,noemblem' }
    )
}
else {
    $variants = @(@{ Name = $Variant; Diag = $Diag })
}

$summaries = @()
foreach ($v in $variants) {
    Write-Host ("Variant {0} (DESKVERVE_DIAG='{1}'): {2} starts" -f $v.Name, $v.Diag, $Count)
    $items = @()
    for ($n = 1; $n -le $Count; $n++) {
        $item = Invoke-Start $v.Name $v.Diag $n
        $items += $item
        Write-Host ("  start {0}: {1}   last mark: {2}" -f $n, $item.Result, $item.Mark)
    }

    $crashes = @($items | Where-Object { $_.Result -ne 'alive' -and $_.Result -ne '0x00000000' }).Count
    $alive = @($items | Where-Object { $_.Result -eq 'alive' }).Count
    $summaries += [pscustomobject]@{
        Variant = $v.Name
        Diag = $v.Diag
        Starts = $items.Count
        Crashes = $crashes
        Alive = $alive
    }
}

Write-Host ""
Write-Host "Summary (CSV: $csvPath)"
$summaries | Format-Table -AutoSize
