param(
    [Parameter(Mandatory = $true)]
    [string]$UnityPath,
    [string]$ArtifactsRoot = (Join-Path $env:LOCALAPPDATA ("Temp\crowrx-json-unity-smoke-" + [Guid]::NewGuid().ToString("N")))
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$project = Join-Path $ArtifactsRoot "project"
$outputs = Join-Path $ArtifactsRoot "players"
New-Item -ItemType Directory -Force -Path $project, $outputs | Out-Null
Copy-Item -Recurse -Force (Join-Path $PSScriptRoot "Assets") $project
Copy-Item -Recurse -Force (Join-Path $PSScriptRoot "Packages") $project
Copy-Item -Recurse -Force (Join-Path $PSScriptRoot "ProjectSettings") $project

$coreRuntime = Join-Path $project "Assets\CrowRx.Json\Runtime"
New-Item -ItemType Directory -Force -Path $coreRuntime | Out-Null
Get-ChildItem (Join-Path $repoRoot "src\CrowRx.Json") -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch "[\\/](obj|bin)[\\/]" } |
    Copy-Item -Destination $coreRuntime
@'
{
  "name": "CrowRx.Json",
  "rootNamespace": "CrowRx.Json",
  "references": [],
  "noEngineReferences": true
}
'@ | Set-Content -Encoding UTF8 (Join-Path $coreRuntime "CrowRx.Json.asmdef")

foreach ($backend in @("Mono", "Il2Cpp")) {
    $method = if ($backend -eq "Mono") { "UnitySmokeBuild.BuildMono" } else { "UnitySmokeBuild.BuildIl2Cpp" }
    $player = Join-Path $outputs ("CrowRxJson" + $backend + ".exe")
    $buildLog = Join-Path $ArtifactsRoot ("build-" + $backend + ".log")
    $buildProcess = Start-Process -FilePath $UnityPath -ArgumentList @(
        "-batchmode", "-nographics", "-quit",
        "-projectPath", $project,
        "-executeMethod", $method,
        "-smokeOutput", $player,
        "-logFile", $buildLog
    ) -Wait -PassThru -WindowStyle Hidden
    if ($buildProcess.ExitCode -ne 0) { throw "Unity $backend build failed. See $buildLog" }

    $runLog = Join-Path $ArtifactsRoot ("run-" + $backend + ".log")
    $runProcess = Start-Process -FilePath $player -ArgumentList @(
        "-batchmode", "-nographics", "-logFile", $runLog
    ) -Wait -PassThru -WindowStyle Hidden
    if ($runProcess.ExitCode -ne 0) { throw "Unity $backend player failed. See $runLog" }
    if (-not (Select-String -Quiet -Path $runLog -Pattern "CROWRX_JSON_UNITY_SMOKE_OK")) {
        throw "Unity $backend player did not report success. See $runLog"
    }
}

Write-Output "UNITY_SMOKE_ROOT=$ArtifactsRoot"
Write-Output "UNITY_MONO_SMOKE=PASS"
Write-Output "UNITY_IL2CPP_SMOKE=PASS"
