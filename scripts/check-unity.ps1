param(
    [string]$UnityHub = "Unity Hub.exe",
    [string]$UnityEditor = "Unity.exe",
    [string]$UnityProjectPath = "unity\EnvForge-local-first",
    [string]$UnityLogFile = "unity-batchmode.log"
)

$ErrorActionPreference = "Stop"

$projectPath = (Resolve-Path -LiteralPath $UnityProjectPath).Path
$logFile = $UnityLogFile
if (-not [System.IO.Path]::IsPathRooted($logFile)) {
    $logFile = Join-Path (Get-Location) $logFile
}

function Invoke-CheckedProcess {
    param(
        [string]$FilePath,
        [string[]]$ArgumentList
    )

    $process = Start-Process `
        -FilePath $FilePath `
        -ArgumentList $ArgumentList `
        -Wait `
        -PassThru `
        -WindowStyle Hidden

    if ($process.ExitCode -ne 0) {
        exit $process.ExitCode
    }
}

Invoke-CheckedProcess `
    -FilePath $UnityHub `
    -ArgumentList @("--", "--headless", "help")

Invoke-CheckedProcess `
    -FilePath $UnityEditor `
    -ArgumentList @(
        "-batchmode",
        "-quit",
        "-nographics",
        "-projectPath",
        $projectPath,
        "-logFile",
        $logFile
    )
