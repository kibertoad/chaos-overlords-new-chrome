[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('linux-x64', 'osx-x64', 'osx-arm64')]
    [string] $Runtime,
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $artifactsRoot "ChaosOverlordsNewChrome-$Runtime"
}
$packageRoot = [IO.Path]::GetFullPath($OutputDirectory)
$artifactsPrefix = $artifactsRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) +
    [IO.Path]::DirectorySeparatorChar
if (-not $packageRoot.StartsWith($artifactsPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Package output must remain below '$artifactsRoot'."
}

& (Join-Path $PSScriptRoot 'Verify-Repository.ps1') -RepositoryRoot $repositoryRoot
if ($LASTEXITCODE -ne 0) { throw 'Repository policy verification failed.' }

if ($Runtime.StartsWith('linux-', [StringComparison]::Ordinal) -and -not $IsLinux) {
    throw "Runtime '$Runtime' must be published on Linux so its executable can be smoke-tested."
}
if ($Runtime.StartsWith('osx-', [StringComparison]::Ordinal) -and -not $IsMacOS) {
    throw "Runtime '$Runtime' must be published on macOS so its executable can be smoke-tested."
}

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
    '--runtime', $Runtime,
    '--self-contained', 'true',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
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
    --output $toolOutput
if ($LASTEXITCODE -ne 0) { throw 'Extractor publish failed.' }
Remove-Item -LiteralPath $buildRoot -Recurse -Force

Copy-Item -LiteralPath (Join-Path $repositoryRoot 'README.md') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'NOTICE') -Destination $packageRoot
if (Test-Path -LiteralPath (Join-Path $gameOutput 'Assets')) {
    throw 'The portable package contains an Assets directory.'
}

$gameExecutable = Join-Path $gameOutput 'Rechaos.Game'
$extractorExecutable = Join-Path $toolOutput 'Rechaos.Extractor'
if (-not (Test-Path -LiteralPath $gameExecutable -PathType Leaf)) {
    throw "Packaged game is missing at '$gameExecutable'."
}
if (-not (Test-Path -LiteralPath $extractorExecutable -PathType Leaf)) {
    throw "Packaged extractor is missing at '$extractorExecutable'."
}
& chmod 755 $gameExecutable $extractorExecutable
if ($LASTEXITCODE -ne 0) { throw 'Could not mark packaged executables as executable.' }
& $gameExecutable --smoke-test
if ($LASTEXITCODE -ne 0) { throw 'Packaged game smoke check failed.' }

Write-Host "Self-contained $Runtime package verified at $packageRoot"
