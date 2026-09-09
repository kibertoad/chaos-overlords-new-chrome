@echo off
setlocal
cd /d "%~dp0"
set "SOURCE=%~1"
if "%SOURCE%"=="" set "SOURCE=C:\GOG Games\Chaos Overlords"
"%~dp0Tools\Rechaos.Extractor.exe" --source "%SOURCE%" --output "%~dp0Game\Assets"
if errorlevel 1 (
    echo.
    echo Resource installation failed. The original installation was not modified.
    pause
    exit /b 1
)
echo.
echo Owned resources are installed locally in Game\Assets.
pause
