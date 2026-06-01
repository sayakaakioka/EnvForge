param(
    [string]$UnityHub = "Unity Hub.exe",
    [string]$UnityEditor = "Unity.exe",
    [string]$UnityProjectPath = "unity\EnvForge-local-first",
    [string]$UnityLogFile = "unity-batchmode.log",
    [int]$TimeoutSeconds = 240,
    [switch]$InitializeUnityHub,
    [switch]$ResolveOnly,
    [switch]$RunBatchmode
)

$ErrorActionPreference = "Stop"

$projectPath = (Resolve-Path -LiteralPath $UnityProjectPath).Path
$logFile = $UnityLogFile
if (-not [System.IO.Path]::IsPathRooted($logFile)) {
    $logFile = Join-Path (Get-Location) $logFile
}

function Resolve-ToolPath {
    param(
        [string]$RequestedPath,
        [string[]]$CandidatePaths,
        [string]$ToolName,
        [switch]$Required
    )

    if ([System.IO.Path]::IsPathRooted($RequestedPath) -and (Test-Path -LiteralPath $RequestedPath)) {
        return (Resolve-Path -LiteralPath $RequestedPath).Path
    }

    $command = Get-Command $RequestedPath -ErrorAction SilentlyContinue
    if ($command -ne $null) {
        return $command.Source
    }

    foreach ($candidatePath in $CandidatePaths) {
        if (-not [string]::IsNullOrWhiteSpace($candidatePath) -and (Test-Path -LiteralPath $candidatePath)) {
            return (Resolve-Path -LiteralPath $candidatePath).Path
        }
    }

    if ($Required) {
        throw "Could not find $ToolName. Pass its path with the matching Makefile variable."
    }

    return $null
}

function Get-UnityEditorCandidates {
    $editorRoot = Join-Path $env:ProgramFiles "Unity\Hub\Editor"
    if (-not (Test-Path -LiteralPath $editorRoot)) {
        return @()
    }

    return Get-ChildItem -LiteralPath $editorRoot -Directory |
        Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName "Editor\Unity.exe" }
}

function Test-UnityLogSucceeded {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }

    $content = Get-Content -LiteralPath $Path -Tail 80 -ErrorAction SilentlyContinue
    return ($content -match "Application will terminate with return code 0") -or
        ($content -match "Exiting batchmode successfully now") -or
        ($content -match "Batchmode quit successfully")
}

function Invoke-CheckedProcess {
    param(
        [string]$FilePath,
        [string[]]$ArgumentList,
        [string]$SuccessLogPath
    )

    $process = Start-Process `
        -FilePath $FilePath `
        -ArgumentList $ArgumentList `
        -PassThru `
        -WindowStyle Hidden

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while (-not $process.HasExited) {
        if (-not [string]::IsNullOrWhiteSpace($SuccessLogPath) -and (Test-UnityLogSucceeded $SuccessLogPath)) {
            return
        }

        if ([DateTime]::UtcNow -ge $deadline) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            throw "Timed out waiting for $FilePath after $TimeoutSeconds seconds."
        }

        Start-Sleep -Seconds 1
    }

    if (-not [string]::IsNullOrWhiteSpace($SuccessLogPath) -and (Test-UnityLogSucceeded $SuccessLogPath)) {
        return
    }

    if ($process.ExitCode -ne 0) {
        exit $process.ExitCode
    }
}

$resolvedUnityHub = Resolve-ToolPath `
    -RequestedPath $UnityHub `
    -ToolName "Unity Hub" `
    -CandidatePaths @(
        (Join-Path $env:ProgramFiles "Unity Hub\Unity Hub.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Unity Hub\Unity Hub.exe")
    )

if ($InitializeUnityHub -and $resolvedUnityHub -ne $null) {
    Invoke-CheckedProcess `
        -FilePath $resolvedUnityHub `
        -ArgumentList @("--", "--headless", "help")
}

$resolvedUnityEditor = Resolve-ToolPath `
    -RequestedPath $UnityEditor `
    -ToolName "Unity Editor" `
    -CandidatePaths @(Get-UnityEditorCandidates) `
    -Required

if ($ResolveOnly -or -not $RunBatchmode) {
    Write-Output "Unity Editor: $resolvedUnityEditor"
    if ($resolvedUnityHub -ne $null) {
        Write-Output "Unity Hub: $resolvedUnityHub"
    }
    else {
        Write-Output "Unity Hub: not found"
    }
    Write-Output "Project: $projectPath"
    Write-Output "Log: $logFile"
    Write-Output "Batchmode: skipped"
    exit 0
}

if (Test-Path -LiteralPath $logFile) {
    Remove-Item -LiteralPath $logFile -Force
}

Invoke-CheckedProcess `
    -FilePath $resolvedUnityEditor `
    -ArgumentList @(
        "-batchmode",
        "-quit",
        "-nographics",
        "-projectPath",
        $projectPath,
        "-logFile",
        $logFile
    ) `
    -SuccessLogPath $logFile
