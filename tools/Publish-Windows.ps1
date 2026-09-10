[CmdletBinding()]
param(
    [string] $OutputDirectory,
    [switch] $SkipArchive
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $artifactsRoot 'ChaosOverlordsNewChrome-win-x64'
}
$packageRoot = [IO.Path]::GetFullPath($OutputDirectory)
$artifactsPrefix = $artifactsRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $packageRoot.StartsWith($artifactsPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Package output must remain below '$artifactsRoot'."
}

& (Join-Path $PSScriptRoot 'Verify-Repository.ps1') -RepositoryRoot $repositoryRoot
if ($LASTEXITCODE -ne 0) { throw 'Repository policy verification failed.' }

if (Test-Path -LiteralPath $packageRoot) {
    $resolvedPackage = (Resolve-Path -LiteralPath $packageRoot).Path
    if (-not $resolvedPackage.StartsWith($artifactsPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove package path outside '$artifactsRoot'."
    }
    Remove-Item -LiteralPath $resolvedPackage -Recurse -Force
}
$gameOutput = Join-Path $packageRoot 'Game'
$toolOutput = Join-Path $packageRoot 'Tools'
$buildRoot = Join-Path $packageRoot '.build'
New-Item -ItemType Directory -Path $gameOutput, $toolOutput -Force | Out-Null

$common = @(
    '--configuration', 'Release',
    '--runtime', 'win-x64',
    '--self-contained', 'true',
    '-p:DebugType=None',
    '-p:DebugSymbols=false',
    '-p:UseSharedCompilation=false',
    '-m:1',
    '--artifacts-path', $buildRoot,
    '--verbosity', 'minimal'
)
& dotnet publish (Join-Path $repositoryRoot 'src/Rechaos.Game/Rechaos.Game.csproj') @common `
    '-p:IncludeOriginalAssets=false' --output $gameOutput
if ($LASTEXITCODE -ne 0) { throw 'Game publish failed.' }
& dotnet publish (Join-Path $repositoryRoot 'src/Rechaos.Extractor/Rechaos.Extractor.csproj') @common `
    '-p:PublishSingleFile=true' --output $toolOutput
if ($LASTEXITCODE -ne 0) { throw 'Extractor publish failed.' }
Remove-Item -LiteralPath $buildRoot -Recurse -Force

Copy-Item -LiteralPath (Join-Path $repositoryRoot 'packaging/windows/Start Chaos Overlords - New Chrome.bat') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'packaging/windows/Install Original Resources.bat') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'README.md') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'NOTICE') -Destination $packageRoot

if (Test-Path -LiteralPath (Join-Path $gameOutput 'Assets/manifest.json')) {
    throw 'The portable package contains extracted original assets.'
}
& (Join-Path $gameOutput 'Rechaos.Game.exe') --smoke-test
if ($LASTEXITCODE -ne 0) { throw 'Packaged game smoke check failed.' }
& (Join-Path $gameOutput 'Rechaos.Game.exe') --platform-smoke-test
if ($LASTEXITCODE -ne 0) { throw 'Packaged game could not initialize its native platform libraries.' }
foreach ($nativeLibrary in @('SDL2.dll', 'openal.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $gameOutput $nativeLibrary))) {
        throw "Packaged game is missing native library '$nativeLibrary'."
    }
}
if (-not (Test-Path -LiteralPath (Join-Path $toolOutput 'Rechaos.Extractor.exe'))) {
    throw 'Packaged extractor is missing.'
}

if (-not $SkipArchive) {
    $archivePath = "$packageRoot.zip"
    if (Test-Path -LiteralPath $archivePath) { Remove-Item -LiteralPath $archivePath -Force }
    Compress-Archive -LiteralPath $packageRoot -DestinationPath $archivePath -CompressionLevel Optimal
    Write-Host "Created $archivePath"
}
Write-Host "Self-contained Windows package verified at $packageRoot"
exit 0
