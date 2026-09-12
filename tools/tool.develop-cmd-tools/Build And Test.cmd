@echo off
setlocal
call "%~dp0Run Tests.cmd" -BuildFirst %*
set "EXITCODE=%ERRORLEVEL%"
endlocal & exit /b %EXITCODE%
