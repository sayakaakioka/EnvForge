param(
    [string]$UnityEditor = "Unity.exe",
    [string]$UnityProjectPath = "unity\EnvForge-local-first",
    [string[]]$AdditionalArguments = @(),
    [switch]$NoPauseOnError
)

$ErrorActionPreference = "Stop"

function Wait-BeforeExitOnError {
    if (-not $NoPauseOnError -and $Host.Name -eq "ConsoleHost" -and [string]::IsNullOrWhiteSpace($env:CI)) {
        Write-Host ""
        Read-Host "Press Enter to close this window"
    }
}

function Resolve-RepoRoot {
    $scriptRoot = $PSScriptRoot
    if ([string]::IsNullOrWhiteSpace($scriptRoot)) {
        $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    }

    $candidateRoot = Resolve-Path -LiteralPath (Join-Path $scriptRoot "..")
    return $candidateRoot.Path
}

function Resolve-ProjectPath {
    param(
        [string]$RequestedPath,
        [string]$RepoRoot
    )

    if ([System.IO.Path]::IsPathRooted($RequestedPath)) {
        return (Resolve-Path -LiteralPath $RequestedPath).Path
    }

    $repoRelativePath = Join-Path $RepoRoot $RequestedPath
    if (Test-Path -LiteralPath $repoRelativePath) {
        return (Resolve-Path -LiteralPath $repoRelativePath).Path
    }

    if (Test-Path -LiteralPath $RequestedPath) {
        return (Resolve-Path -LiteralPath $RequestedPath).Path
    }

    throw "Could not find Unity project path '$RequestedPath'. Checked repo root '$RepoRoot' and current directory '$((Get-Location).Path)'."
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
        throw "Could not find $ToolName. Pass its path with the matching argument or Makefile variable."
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

try {
    $repoRoot = Resolve-RepoRoot
    $projectPath = Resolve-ProjectPath `
        -RequestedPath $UnityProjectPath `
        -RepoRoot $repoRoot

    $resolvedUnityEditor = Resolve-ToolPath `
        -RequestedPath $UnityEditor `
        -ToolName "Unity Editor" `
        -CandidatePaths @(Get-UnityEditorCandidates) `
        -Required

    $arguments = @(
        "-projectPath",
        $projectPath,
        "-force-d3d11"
    ) + $AdditionalArguments

    $process = Start-Process `
        -FilePath $resolvedUnityEditor `
        -ArgumentList $arguments `
        -PassThru

    Write-Output "Unity Editor: $resolvedUnityEditor"
    Write-Output "Project: $projectPath"
    Write-Output "Graphics API: Direct3D 11 (-force-d3d11)"
    Write-Output "Process ID: $($process.Id)"
}
catch {
    Write-Error $_
    Wait-BeforeExitOnError
    exit 1
}
