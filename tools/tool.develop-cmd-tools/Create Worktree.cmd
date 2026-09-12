@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\New-InitializedWorktree.ps1" %*
set "EXITCODE=%ERRORLEVEL%"
if not "%EXITCODE%"=="0" if not "%DEVELOP_CMD_NO_PAUSE%"=="1" pause
endlocal & exit /b %EXITCODE%
