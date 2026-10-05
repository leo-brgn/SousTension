# Finition PBR de TOUS les modèles (matériaux + UV + textures cuites + rendu) : une instance Blender par dossier de kit, 3 en parallèle.
# Utilisation (racine du dépôt) :  powershell -File blender/finish_all.ps1 [-Out build_pbr] [-Parallel 3] [-MaxRes 1024] [-Kits Instruments,Reactor]
# Reprend où il s'est arrêté (-skip-existing). Journaux : <Out>/logs/<Kit>.log. Les FBX d'origine ne sont jamais modifiés.
param(
    [string]$Out = "build_pbr",
    [int]$Parallel = 3,
    [int]$MaxRes = 1024,
    [string[]]$Kits = @(),
    [string]$Blender = "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$outAbs = Join-Path $root $Out
New-Item -ItemType Directory -Force (Join-Path $outAbs "logs") | Out-Null
$models = Join-Path $root "Assets\_Project\Art\Models"
$dirs = Get-ChildItem $models -Directory | Where-Object { (Get-ChildItem $_.FullName -Filter *.fbx -File).Count -gt 0 }
if ($Kits.Count -gt 0) { $dirs = $dirs | Where-Object { $Kits -contains $_.Name } }
# les plus gros dossiers d'abord : meilleur équilibrage des 3 files
$dirs = $dirs | Sort-Object { (Get-ChildItem $_.FullName -Filter *.fbx -File).Count } -Descending
$running = @()
foreach ($d in $dirs) {
    while (($running | Where-Object { -not $_.HasExited }).Count -ge $Parallel) { Start-Sleep -Seconds 3 }
    $log = Join-Path $outAbs ("logs\" + $d.Name + ".log")
    $argString = "--background --factory-startup --python-exit-code 1 --python `"$PSScriptRoot\finish.py`" -- --kit $($d.Name) --in `"$($d.FullName)`" --out `"$outAbs`" --skip-existing --max-res $MaxRes"
    Write-Host "== $($d.Name) ($((Get-ChildItem $d.FullName -Filter *.fbx -File).Count) modèles)"
    $running += Start-Process -FilePath $Blender -ArgumentList $argString -RedirectStandardOutput $log -RedirectStandardError "$log.err" -WindowStyle Hidden -PassThru
}
$running | Wait-Process
Write-Host "terminé. Résumé :"
Get-ChildItem (Join-Path $outAbs "logs") -Filter *.log | ForEach-Object { Select-String -Path $_.FullName -Pattern "^KIT " | ForEach-Object { $_.Line } }
