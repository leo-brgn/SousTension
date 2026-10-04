# Génère tous les modèles du jeu avec Blender 4.0 (aucun asset téléchargé) et les écrit dans Assets/_Project/Art/Models/.
# Utilisation (depuis la racine du dépôt) :   powershell -File blender/build.ps1 [-Preview]
# -Preview : écrit aussi des aperçus PNG pièce par pièce dans scratch_out/preview/<kit>/ (non commités).
param(
    [switch]$Preview,
    [string]$Blender = "C:\Program Files\Blender Foundation\Blender 4.0\blender.exe"
)
$ErrorActionPreference = "Stop"
if (-not (Test-Path $Blender)) { throw "Blender introuvable : $Blender (installer Blender 4.0 ou passer -Blender <chemin>)" }
$root = Split-Path -Parent $PSScriptRoot
$kits = @(
    @{ Script = "kit_instruments.py"; Out = "Instruments" },
    @{ Script = "kit_reactor.py";     Out = "Reactor" },
    @{ Script = "kit_structure.py";   Out = "Structure" }
)
foreach ($k in $kits) {
    $outDir = Join-Path $root ("Assets\_Project\Art\Models\" + $k.Out)
    New-Item -ItemType Directory -Force $outDir | Out-Null
    $args = @("--background", "--factory-startup", "--python", (Join-Path $PSScriptRoot $k.Script), "--", $outDir)
    if ($Preview) {
        $prev = Join-Path $root ("scratch_out\preview\" + $k.Out)
        New-Item -ItemType Directory -Force $prev | Out-Null
        $args += @("--preview", $prev)
    }
    Write-Host "== $($k.Script) -> $outDir"
    & $Blender @args | Select-String -Pattern "^EXPORT|^PREVIEW|Error|Traceback"
    if ($LASTEXITCODE -ne 0) { throw "Blender a échoué pour $($k.Script)" }
}
