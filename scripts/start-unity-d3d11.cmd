@echo off
setlocal

set SCRIPT_DIR=%~dp0
set PS_SCRIPT=%SCRIPT_DIR%start-unity-d3d11.ps1

echo Starting EnvForge Unity Editor with Direct3D 11...
echo Script: %PS_SCRIPT%
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%" -NoPauseOnError
set EXIT_CODE=%ERRORLEVEL%

echo.
if not "%EXIT_CODE%"=="0" (
    echo Failed to start Unity. Exit code: %EXIT_CODE%
) else (
    echo Launch command finished. If Unity is already open for this project, Unity may not open a second editor window.
)

echo.
pause
exit /b %EXIT_CODE%
