@echo off
setlocal
set "PROJECT_ROOT_ARGUMENT="
if defined DEVELOP_CMD_PROJECT_ROOT set PROJECT_ROOT_ARGUMENT=-ProjectRoot "%DEVELOP_CMD_PROJECT_ROOT%"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Invoke-TestOperations.ps1" %PROJECT_ROOT_ARGUMENT% %*
set "EXITCODE=%ERRORLEVEL%"
if not "%EXITCODE%"=="0" (
    echo.
    echo Test workflow failed with exit code %EXITCODE%. Review the error above.
    pause
)
endlocal & exit /b %EXITCODE%
