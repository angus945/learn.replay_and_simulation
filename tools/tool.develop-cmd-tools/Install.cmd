@echo off
setlocal
set "PROJECT_ROOT=%CD%"
if not "%~1"=="" set "PROJECT_ROOT=%~f1"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Invoke-Install.ps1" -ProjectRoot "%PROJECT_ROOT%"
set "EXITCODE=%ERRORLEVEL%"
endlocal & exit /b %EXITCODE%
