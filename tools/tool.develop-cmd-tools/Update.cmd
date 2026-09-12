@echo off
setlocal
set "TEMP_UPDATER=%TEMP%\develop-cmd-tools-update-%RANDOM%%RANDOM%.ps1"
copy /y "%~dp0scripts\Update-Self.ps1" "%TEMP_UPDATER%" >nul || exit /b 1
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%TEMP_UPDATER%" -ToolRoot "%~dp0"
set "EXITCODE=%ERRORLEVEL%"
del "%TEMP_UPDATER%" >nul 2>&1
endlocal & exit /b %EXITCODE%
