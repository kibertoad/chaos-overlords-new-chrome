@echo off
setlocal
cd /d "%~dp0"
set "SOURCE=%~1"
if "%SOURCE%"=="" set "SOURCE=C:\GOG Games\Chaos Overlords"
"%~dp0Tools\Rechaos.Extractor.exe" --source "%SOURCE%" --output "%~dp0Game\Assets"
if errorlevel 1 (
    echo.
    echo Asset import failed. Your original Chaos Overlords installation was not modified.
    pause
    exit /b 1
)
echo.
echo Art, music, sound, video, and other assets from your legal copy are installed in Game\Assets.
pause
