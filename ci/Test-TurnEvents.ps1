param(
    [string]$UnityPath = $env:UNITY_EXE,
    [string]$LogPath
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$versionText = Get-Content (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') -Raw
$unityVersion = [regex]::Match($versionText, '(?m)^m_EditorVersion: (\S+)').Groups[1].Value

if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    $candidates = @(
        "C:/Program Files/Unity/Hub/Editor/$unityVersion/Editor/Unity.exe",
        "$env:USERPROFILE/Desktop/unity/$unityVersion/Editor/Unity.exe"
    )
    $UnityPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($UnityPath) -or !(Test-Path -LiteralPath $UnityPath)) {
    throw "Unity $unityVersion was not found. Pass -UnityPath with the full path to Unity.exe."
}
if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path $projectRoot 'Logs/turn-event-boundaries.log'
}
$LogPath = [System.IO.Path]::GetFullPath($LogPath)
New-Item -ItemType Directory -Force -Path (Split-Path $LogPath -Parent) | Out-Null
if (Test-Path -LiteralPath $LogPath) { Remove-Item -LiteralPath $LogPath }

Write-Host "Project: $projectRoot"
Write-Host "Unity: $UnityPath"
Write-Host "Log: $LogPath"
$unityArguments = @(
    '-batchmode', '-nographics', '-quit',
    '-projectPath', "`"$projectRoot`"",
    '-executeMethod', 'BattleTurnEventChecks.RunBoundaries',
    '-logFile', "`"$LogPath`""
)
$unityProcess = Start-Process -FilePath $UnityPath -ArgumentList $unityArguments -WindowStyle Hidden -Wait -PassThru

if (!(Test-Path -LiteralPath $LogPath)) {
    throw "Unity did not create a log. Exit code: $($unityProcess.ExitCode)."
}
$logText = Get-Content -LiteralPath $LogPath -Raw
if ($unityProcess.ExitCode -ne 0 -or $logText -notmatch 'TURN_BOUNDARY_CHECKS_PASSED:') {
    Get-Content -LiteralPath $LogPath -Tail 100 | Write-Host
    throw "Turn boundary checks failed. Unity exit code: $($unityProcess.ExitCode). See $LogPath"
}
Select-String -LiteralPath $LogPath -Pattern 'TURN_BOUNDARY_.*PASSED:' |
    ForEach-Object { Write-Host $_.Line }
