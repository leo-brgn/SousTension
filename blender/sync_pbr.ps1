# Copie les modèles et textures PBR générés (build_pbr/) vers le projet Unity : Assets/_Project/Art/PBR/{Models,Textures}/<Kit>/.
# Les rendus (build_pbr/Renders) et rapports restent hors du projet. Utilisation : powershell -File blender/sync_pbr.ps1 [-From build_pbr]
param([string]$From = "build_pbr")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root $From
$dst = Join-Path $root "Assets\_Project\Art\PBR"
foreach ($sub in "Models", "Textures") {
    if (-not (Test-Path (Join-Path $src $sub))) { throw "introuvable : $src\$sub (lancer blender/finish_all.ps1 d'abord)" }
    New-Item -ItemType Directory -Force (Join-Path $dst $sub) | Out-Null
    Copy-Item (Join-Path $src "$sub\*") (Join-Path $dst $sub) -Recurse -Force
}
Write-Host ("copié : {0} modèles, {1} textures" -f (Get-ChildItem "$dst\Models" -Recurse -Filter *.fbx).Count, (Get-ChildItem "$dst\Textures" -Recurse -Filter *.png).Count)
