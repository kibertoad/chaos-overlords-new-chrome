[CmdletBinding()]
param(
    [ValidateRange(1, 16)]
    [int] $MaxCpuCount = 2,
    [switch] $ShutdownBuildServersAfterRun
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$sha256 = [Security.Cryptography.SHA256]::Create()
try {
    $repositoryHash = $sha256.ComputeHash(
        [Text.Encoding]::UTF8.GetBytes($repositoryRoot.ToUpperInvariant()))
}
finally {
    $sha256.Dispose()
}
$repositoryIdentity = [BitConverter]::ToString($repositoryHash).Replace('-', '')
$lockPath = Join-Path $temporaryRoot "rechaos-validation-$($repositoryIdentity.Substring(0, 16)).lock"
$lock = $null

function Invoke-CheckedDotnet {
    param([Parameter(Mandatory = $true)][string[]] $Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments[0]) failed with exit code $LASTEXITCODE."
    }
}

function Stop-CheckoutGame {
    $repositoryPrefix = $repositoryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) +
        [IO.Path]::DirectorySeparatorChar
    foreach ($process in @(Get-Process -Name 'Rechaos.Game' -ErrorAction SilentlyContinue)) {
        try {
            $processPath = [IO.Path]::GetFullPath($process.Path)
        }
        catch {
            Write-Warning "Could not inspect Rechaos.Game process $($process.Id); leaving it running."
            continue
        }

        if (-not $processPath.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        Write-Host "Stopping checkout game process $($process.Id) before validation."
        Stop-Process -Id $process.Id -Force
        Wait-Process -Id $process.Id -ErrorAction SilentlyContinue
    }
}

try {
    try {
        $lock = [IO.File]::Open(
            $lockPath,
            [IO.FileMode]::OpenOrCreate,
            [IO.FileAccess]::ReadWrite,
            [IO.FileShare]::None)
    }
    catch [IO.IOException] {
        throw "Another validation run is already active for this checkout ($lockPath)."
    }

    Stop-CheckoutGame

    & (Join-Path $PSScriptRoot 'Verify-Repository.ps1') -RepositoryRoot $repositoryRoot
    if ($LASTEXITCODE -ne 0) { throw 'Repository policy verification failed.' }

    $msbuildArguments = @(
        "-maxCpuCount:$MaxCpuCount",
        '-nodeReuse:true',
        '--verbosity', 'minimal'
    )
    Invoke-CheckedDotnet -Arguments (@(
        'restore', (Join-Path $repositoryRoot 'Rechaos.slnx')
    ) + $msbuildArguments)
    Invoke-CheckedDotnet -Arguments (@(
            'build', (Join-Path $repositoryRoot 'Rechaos.slnx'),
            '--configuration', 'Release',
            '--no-restore',
            '-p:IncludeOriginalAssets=false'
    ) + $msbuildArguments)
    Invoke-CheckedDotnet -Arguments (@(
        'test',
        '--project', (Join-Path $repositoryRoot 'tests/Rechaos.Tests/Rechaos.Tests.csproj'),
        '--configuration', 'Release',
        '--no-build',
        '--no-restore',
        '--no-progress',
        '--minimum-expected-tests', '354',
        '--verbosity', 'minimal'
    ))
}
finally {
    if ($ShutdownBuildServersAfterRun) {
        Write-Host 'Stopping .NET build servers for the current user.'
        & dotnet build-server shutdown
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "dotnet build-server shutdown returned exit code $LASTEXITCODE."
        }
    }

    if ($lock) {
        $lock.Dispose()
    }
}
