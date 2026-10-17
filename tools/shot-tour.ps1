# Screenshot tour of the 3D boat (E10-01): starts the local server in debug mode (if it is not running), runs the development client with --tour and
# leaves one PNG per viewpoint in Builds\Shots (see ShotTourView for the list of viewpoints).
# Build the client first from Unity: menu SousTension > Spike > 4. Build the Windows development client.
# Usage (repo root):  powershell -ExecutionPolicy Bypass -File tools/shot-tour.ps1 [-Build Builds\Dev\SousTension.exe] [-Out Builds\Shots]
param(
    [string]$Build = "Builds\Dev\SousTension.exe",
    [string]$Out = "Builds\Shots"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
if (-not (Test-Path $Build)) { throw "Build not found: $Build. Build it from Unity first." }
try { $up = (Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:7350/healthcheck" -TimeoutSec 2).StatusCode -eq 200 } catch { $up = $false }
if (-not $up) { & "$PSScriptRoot\play-local.ps1" -ServerOnly }
$full = Join-Path $root $Out
Remove-Item $full -Recurse -Force -ErrorAction SilentlyContinue
$exe = (Resolve-Path $Build).Path
# the path of --tour may contain spaces: quote the whole argument
$p = Start-Process -FilePath $exe -ArgumentList @("--device=tour$((Get-Random -Minimum 100000 -Maximum 999999))", "`"--tour=$full`"", "-screen-fullscreen", "0", "-screen-width", "1280", "-screen-height", "720") -PassThru
if (-not $p.WaitForExit(180000)) { $p.Kill(); throw "the tour did not finish within 3 minutes" }
Get-ChildItem $full -Filter *.png | Select-Object Name, Length | Format-Table -AutoSize
