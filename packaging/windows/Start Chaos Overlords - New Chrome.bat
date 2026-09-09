@echo off
setlocal
cd /d "%~dp0"
"%~dp0Game\Rechaos.Game.exe"
if errorlevel 1 (
  echo.
  echo Chaos Overlords: New Chrome did not start successfully.
  echo Review the error dialog or %%LOCALAPPDATA%%\ChaosOverlordsNewChrome\Logs\startup-error.log.
  pause
  exit /b 1
)
exit /b 0
