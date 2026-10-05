# Génère les modèles du jeu avec Blender 5.2 et les écrit dans Assets/_Project/Art/Models/.
# Utilisation (depuis la racine du dépôt) :   powershell -File blender/build.ps1 [-Preview]
# -Preview : écrit aussi des aperçus PNG pièce par pièce dans scratch_out/preview/<kit>/ (non commités).
param(
    [switch]$Preview,
    [string]$Blender = "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe",
    [ValidateSet("Instruments", "Reactor", "Structure", "Primary", "Machines", "Central", "Tools", "Damage", "Crew", "Lighting", "Radio", "Living", "Signage", "Torpedoes", "Cargo", "Fixtures", "World", "CrewVariants", "Environment")]
    [string[]]$Kit = @("Instruments", "Reactor", "Structure", "Primary", "Machines", "Central", "Tools", "Damage", "Crew", "Lighting", "Radio", "Living", "Signage", "Torpedoes", "Cargo", "Fixtures", "World", "CrewVariants", "Environment")
)
$ErrorActionPreference = "Stop"
if (-not (Test-Path $Blender)) { throw "Blender introuvable : $Blender (installer Blender 5.2 ou passer -Blender <chemin>)" }
$root = Split-Path -Parent $PSScriptRoot
$kits = @(
    @{ Script = "kit_instruments.py"; Out = "Instruments" },
    @{ Script = "kit_reactor.py";     Out = "Reactor" },
    @{ Script = "kit_structure.py";   Out = "Structure" },
    @{ Script = "kit_primary.py";     Out = "Primary" },
    @{ Script = "kit_machines.py";    Out = "Machines" },
    @{ Script = "kit_central.py";     Out = "Central" },
    @{ Script = "kit_tools.py";       Out = "Tools" },
    @{ Script = "kit_damage.py";      Out = "Damage" },
    @{ Script = "kit_crew.py";        Out = "Crew" },
    @{ Script = "kit_lighting.py";    Out = "Lighting" },
    @{ Script = "kit_radio.py";       Out = "Radio" },
    @{ Script = "kit_living.py";      Out = "Living" },
    @{ Script = "kit_signage.py";     Out = "Signage" },
    @{ Script = "kit_torpedoes.py";   Out = "Torpedoes" },
    @{ Script = "kit_cargo.py";       Out = "Cargo" },
    @{ Script = "kit_fixtures.py";    Out = "Fixtures" },
    @{ Script = "kit_world.py";       Out = "World" },
    @{ Script = "kit_crew_variants.py"; Out = "CrewVariants" },
    @{ Script = "kit_environment.py"; Out = "Environment" }
)
foreach ($k in $kits) {
    if ($k.Out -notin $Kit) { continue }
    $outDir = Join-Path $root ("Assets\_Project\Art\Models\" + $k.Out)
    New-Item -ItemType Directory -Force $outDir | Out-Null
    $args = @("--background", "--factory-startup", "--python-exit-code", "1", "--python", (Join-Path $PSScriptRoot $k.Script), "--", $outDir)
    if ($Preview) {
        $prev = Join-Path $root ("scratch_out\preview\" + $k.Out)
        New-Item -ItemType Directory -Force $prev | Out-Null
        $previewTarget = if ($k.Out -eq "Instruments") { Join-Path $prev "sheet.png" } else { $prev }
        $args += @("--preview", $previewTarget)
    }
    Write-Host "== $($k.Script) -> $outDir"
    & $Blender @args | Select-String -Pattern "^EXPORT|^PREVIEW|Error|Traceback"
    if ($LASTEXITCODE -ne 0) { throw "Blender a échoué pour $($k.Script)" }
}
