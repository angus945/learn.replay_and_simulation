@echo off
setlocal
set "PROJECT_ROOT_ARGUMENT="
if defined DEVELOP_CMD_PROJECT_ROOT set PROJECT_ROOT_ARGUMENT=-ProjectRoot "%DEVELOP_CMD_PROJECT_ROOT%"
if "%~1"=="" (
    pwsh.exe -NoLogo -NoProfile -File "%~dp0scripts\Select-LocalCi.ps1" %PROJECT_ROOT_ARGUMENT%
) else (
    pwsh.exe -NoLogo -NoProfile -File "%~dp0scripts\Invoke-LocalCi.ps1" %PROJECT_ROOT_ARGUMENT% %*
)
set "EXITCODE=%ERRORLEVEL%"
if not "%EXITCODE%"=="0" if not defined DEVELOP_CMD_NO_PAUSE (
    echo.
    echo Local CI failed with exit code %EXITCODE%. Review the report or error above.
    pause
)
endlocal & exit /b %EXITCODE%
