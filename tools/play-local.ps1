# Local playtest launcher (E1-11): starts the Nakama server (debug mode) and launches extra clients of the development build.
# Usage (repo root):  powershell -ExecutionPolicy Bypass -File tools/play-local.ps1 [-Clients 1] [-ServerOnly] [-Build Builds\Dev\SousTension.exe]
# Build the client first from Unity: menu SousTension > Spike > 4. Build the Windows development client.
# The Unity editor is player 1 (menu SousTension > Spike > 3. Play locally); this script launches the other players.
param(
    [int]$Clients = 1,
    [switch]$ServerOnly,
    [string]$Build = "Builds\Dev\SousTension.exe"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "Starting the local server (docker compose)..."
docker compose -f server/docker-compose.yml up -d
if ($LASTEXITCODE -ne 0) { throw "docker compose failed: is Docker started?" }

Write-Host "Waiting for Nakama on http://127.0.0.1:7350 ..."
$up = $false
for ($i = 0; $i -lt 90 -and -not $up; $i++) {
    try { $up = (Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:7350/healthcheck" -TimeoutSec 2).StatusCode -eq 200 } catch { Start-Sleep -Seconds 1 }
}
if (-not $up) { throw "Nakama did not answer within 90 s" }
Write-Host "Server ready (debug mode: the developer console works, key left of 1)."

if ($ServerOnly) { return }
if (-not (Test-Path $Build)) { throw "Build not found: $Build. Build it from Unity: SousTension > Spike > 4. Build the Windows development client." }
for ($n = 1; $n -le $Clients; $n++) {
    $device = "devclient{0}0000" -f $n                      # Nakama needs 10 to 128 characters
    Write-Host "Launching client $n ($device)"
    Start-Process -FilePath $Build -ArgumentList @("--device=$device", "-screen-fullscreen", "0", "-screen-width", "1280", "-screen-height", "720")
}
Write-Host "Done. Extra options of the client: --latency=MS --jitter=MS --loss=PERCENT (see MovingFrameBootstrap)."
