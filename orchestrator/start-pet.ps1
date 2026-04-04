param(
    [ValidateSet("advise", "auto", "pause")]
    [string]$Mode = "advise",
    [int]$WaitTimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$python = "C:\Users\colezhang\AppData\Local\Programs\Python\Python312\python.exe"
$healthUrl = "http://127.0.0.1:15526/api/v1/pet/status"
$outLog = Join-Path $env:USERPROFILE "sts2-pet-orchestrator.out.log"
$errLog = Join-Path $env:USERPROFILE "sts2-pet-orchestrator.err.log"

Write-Host "Waiting for game bridge: $healthUrl"
$deadline = (Get-Date).AddSeconds($WaitTimeoutSeconds)
while ((Get-Date) -lt $deadline) {
    try {
        $null = Invoke-WebRequest -UseBasicParsing -Uri $healthUrl -TimeoutSec 2
        break
    }
    catch {
        Start-Sleep -Milliseconds 1000
    }
}

if ((Get-Date) -ge $deadline) {
    throw "Timed out waiting for the game bridge. Start the game with the mod enabled first."
}

Get-CimInstance Win32_Process -Filter "Name = 'python.exe'" |
    Where-Object { $_.CommandLine -like "*sts2_pet.cli*" } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force }

if (Test-Path -LiteralPath $outLog) { Remove-Item -LiteralPath $outLog -Force }
if (Test-Path -LiteralPath $errLog) { Remove-Item -LiteralPath $errLog -Force }

$process = Start-Process `
    -FilePath $python `
    -WorkingDirectory $scriptDir `
    -ArgumentList "-m", "sts2_pet.cli", "--mode", $Mode `
    -RedirectStandardOutput $outLog `
    -RedirectStandardError $errLog `
    -PassThru

Write-Host "STS2 pet started."
Write-Host "Mode: $Mode"
Write-Host "PID: $($process.Id)"
Write-Host "Out log: $outLog"
Write-Host "Err log: $errLog"
