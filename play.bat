@echo off
setlocal
pushd "%~dp0"

set "ASSET_DIR=src\Rechaos.Game\Assets"
if exist "%ASSET_DIR%\manifest.json" (
  dotnet run --project src\Rechaos.Extractor -- --verify-output --output "%ASSET_DIR%" --quick >nul 2>nul
  if not errorlevel 1 goto launch
)

set "GAME_SOURCE=%CHAOS_OVERLORDS_PATH%"
if not defined GAME_SOURCE set "GAME_SOURCE=C:\GOG Games\Chaos Overlords"

if not exist "%GAME_SOURCE%\DATA\PX16" (
  echo Original Chaos Overlords assets were not found.
  echo Set CHAOS_OVERLORDS_PATH to your legal installation directory, then run play.bat again.
  popd
  exit /b 1
)

echo Creating the local asset pack from "%GAME_SOURCE%"...
dotnet run --project src\Rechaos.Extractor -- --source "%GAME_SOURCE%" --output "%ASSET_DIR%"
if errorlevel 1 (
  popd
  exit /b 1
)

:launch
dotnet run --project src\Rechaos.Game
set "GAME_EXIT=%ERRORLEVEL%"
popd
exit /b %GAME_EXIT%
