@echo off
setlocal
cd /d "%~dp0"
"%~dp0Game\Rechaos.Game.exe"
exit /b %ERRORLEVEL%
