@echo off
setlocal

set "LOGDIR=%ProgramData%\Microsoft\IntuneManagementExtension\Logs"
if not exist "%LOGDIR%" mkdir "%LOGDIR%"
set "LAUNCHERLOG=%LOGDIR%\MakeMeAdmin-launcher.log"

echo %DATE% %TIME% Install.cmd started from "%~dp0".>>"%LAUNCHERLOG%"
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%~dp0Install-MakeMeAdmin.ps1" >>"%LAUNCHERLOG%" 2>&1
set "RESULT=%ERRORLEVEL%"
echo %DATE% %TIME% Install-MakeMeAdmin.ps1 returned %RESULT%.>>"%LAUNCHERLOG%"
exit /b %RESULT%
