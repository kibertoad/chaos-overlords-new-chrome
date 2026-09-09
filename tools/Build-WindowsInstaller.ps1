[CmdletBinding()]
param(
    [string] $Version = '0.1.0',
    [string] $Compiler,
    [switch] $SkipPackage
)

$ErrorActionPreference = 'Stop'
$requiredCompilerVersion = '7.1.0'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not $SkipPackage) {
    & (Join-Path $PSScriptRoot 'Publish-Windows.ps1') -SkipArchive
    if ($LASTEXITCODE -ne 0) { throw 'Portable package creation failed.' }
}

$packageRoot = Join-Path $repositoryRoot 'artifacts/ChaosOverlordsNewChrome-win-x64'
if (-not (Test-Path -LiteralPath (Join-Path $packageRoot 'Game/Rechaos.Game.exe'))) {
    throw "The verified portable package is missing at '$packageRoot'."
}

if (-not $Compiler) {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 7/ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7/ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 7/ISCC.exe')
    )
    $Compiler = $candidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
}
if (-not $Compiler) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { $Compiler = $command.Source }
}
if (-not $Compiler -or -not (Test-Path -LiteralPath $Compiler)) {
    throw "Inno Setup $requiredCompilerVersion compiler (ISCC.exe) was not found. Install it or pass -Compiler."
}
$compilerVersion = (& $Compiler --version | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $compilerVersion -ne $requiredCompilerVersion) {
    throw "Inno Setup $requiredCompilerVersion is required; '$Compiler' reports '$compilerVersion'."
}
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Invalid installer version '$Version'; expected x.y.z."
}

$script = Join-Path $repositoryRoot 'packaging/windows/RechaosOverlords.iss'
& $Compiler "/DMyAppVersion=$Version" $script
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }

$installer = Join-Path $repositoryRoot "artifacts/ChaosOverlords-NewChrome-Setup-$Version.exe"
if (-not (Test-Path -LiteralPath $installer)) { throw "Expected installer was not created at '$installer'." }
Write-Host "Windows installer created at $installer"
