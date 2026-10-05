param(
    [switch]$BakeOnly,
    [switch]$CaptureOnly,
    [string]$Unity='C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe',
    [string]$Python='C:\Program Files\Blender Foundation\Blender 5.2\5.2\python\bin\python.exe'
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$method=if ($CaptureOnly) {'AllAssetLightingWorkflow.Capture'} elseif ($BakeOnly) {'AllAssetLightingWorkflow.Bake'} else {'AllAssetLightingWorkflow.Run'}
$marker=if ($CaptureOnly) {'ALL_ASSET_CAPTURE_PASS'} elseif ($BakeOnly) {'ALL_ASSET_BAKE_COMPLETE'} else {'ALL_ASSET_LIGHTING_WORKFLOW_PASS'}
$logPath=Join-Path $projectRoot 'scratch_out/all-asset-lighting.log'
New-Item -ItemType Directory -Force (Split-Path -Parent $logPath) | Out-Null
$unityJob=Start-Process -FilePath $Unity -ArgumentList @('-batchmode','-quit','-projectPath',('"'+$projectRoot+'"'),'-executeMethod',('SousTension.EditorTools.'+$method),'-logFile',('"'+$logPath+'"')) -WindowStyle Hidden -PassThru
$unityJob.WaitForExit(); $unityJob.Refresh()
if ($unityJob.ExitCode -ne 0 -or !(Select-String -LiteralPath $logPath -SimpleMatch $marker -Quiet)) { throw ('Unity workflow failed. See '+$logPath) }
Write-Host $marker
if (!$BakeOnly) {
    & $Python (Join-Path $projectRoot 'tools/build_all_asset_index.py') --root $projectRoot
    if ($LASTEXITCODE -ne 0) { throw 'Screenshot index validation failed' }
}
