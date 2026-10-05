# Workflow déterministe : exports optionnels, pointage GDD, validation Unity, captures optionnelles.
param(
    [switch]$Rebuild,
    [switch]$Preview,
    [switch]$Capture,
    [string]$Blender="C:\Program Files\Blender Foundation\Blender 5.2\blender.exe",
    [string]$Unity="C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe"
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    if ($Rebuild) { & (Join-Path $PSScriptRoot 'build.ps1') -Blender $Blender -Preview:$Preview }
    & $Blender --background --factory-startup --python-exit-code 1 --python (Join-Path $PSScriptRoot 'validate_v1_coverage.py')
    if ($LASTEXITCODE -ne 0) { throw 'Pointage GDD V1 incomplet' }
    New-Item -ItemType Directory -Force (Join-Path $projectRoot 'scratch_out') | Out-Null
    $methods=@(@{Name='V1AssetBuild';Pass='V1_ASSET_BUILD_PASS'})
    if ($Capture) { $methods+=@(@{Name='EnvironmentCapture';Pass='ENVIRONMENT_CAPTURE_PASS'},@{Name='V1AssetCapture';Pass='V1_CAPTURE_PASS'}) }
    foreach ($method in $methods) {
        $logPath=Join-Path $projectRoot ('scratch_out/workflow-'+$method.Name+'.log')
        $arguments=@('-batchmode','-quit','-projectPath',('"'+$projectRoot+'"'),'-executeMethod',('SousTension.EditorTools.'+$method.Name+'.Run'),'-logFile',('"'+$logPath+'"'))
        $unityJob=Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
        $unityJob.WaitForExit();$unityJob.Refresh()
        if ($unityJob.ExitCode -ne 0 -or !(Select-String -LiteralPath $logPath -SimpleMatch $method.Pass -Quiet)) {
            throw ('Unity a échoué pour '+$method.Name+'. Lire '+$logPath)
        }
        Write-Host ($method.Name+' : PASS')
    }
    Write-Host 'V1_WORKFLOW_PASS'
} finally { Pop-Location }
