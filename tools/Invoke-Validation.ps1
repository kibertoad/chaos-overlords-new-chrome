[CmdletBinding()]
param(
    [ValidateRange(1, 16)]
    [int] $MaxCpuCount = 2,
    [switch] $ShutdownBuildServersAfterRun,
    [string] $TestFilter,
    [switch] $IncludeLongRunningTests,
    [switch] $LongRunningTestsOnly,
    [switch] $TraceTestOutput
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$sha256 = [Security.Cryptography.SHA256]::Create()
try {
    if (($TestFilter -and ($IncludeLongRunningTests -or $LongRunningTestsOnly)) -or
        ($IncludeLongRunningTests -and $LongRunningTestsOnly)) {
        throw 'Choose only one of -TestFilter, -IncludeLongRunningTests, or -LongRunningTestsOnly.'
    }

    $repositoryHash = $sha256.ComputeHash(
        [Text.Encoding]::UTF8.GetBytes($repositoryRoot.ToUpperInvariant()))
}
finally {
    $sha256.Dispose()
}
$repositoryIdentity = [BitConverter]::ToString($repositoryHash).Replace('-', '')
$lockPath = Join-Path $temporaryRoot "rechaos-validation-$($repositoryIdentity.Substring(0, 16)).lock"
$validationBuildRoot = Join-Path $temporaryRoot (
    "rechaos-validation-$($repositoryIdentity.Substring(0, 16))-$([Guid]::NewGuid().ToString('N'))")
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

    # A run killed before its `finally` (an agent timeout, a closed terminal) leaves a whole Release
    # build tree behind in %TEMP%, and nothing ever removed it. Holding the lock proves no other run
    # for this checkout owns these, so they are this run's to sweep.
    foreach ($stale in @(Get-ChildItem -Path $temporaryRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like "rechaos-validation-$($repositoryIdentity.Substring(0, 16))-*" })) {
        try {
            Remove-Item -LiteralPath $stale.FullName -Recurse -Force -ErrorAction Stop
            Write-Host "Removed a validation build root left by an interrupted run: $($stale.Name)"
        }
        catch {
            Write-Warning "Could not remove the stale validation build root $($stale.Name)."
        }
    }

    New-Item -ItemType Directory -Path $validationBuildRoot -Force | Out-Null
    $validationProjectRoot = [IO.Path]::GetFullPath($validationBuildRoot) +
        [IO.Path]::DirectorySeparatorChar

    & (Join-Path $PSScriptRoot 'Verify-Repository.ps1') -RepositoryRoot $repositoryRoot
    if ($LASTEXITCODE -ne 0) { throw 'Repository policy verification failed.' }

    # The generated documentation indexes and the links between documents. Checked before the
    # build because it takes a second, but a failure is only raised after the tests, so a stale
    # index never hides a build or test result. Node.js is optional on a developer machine;
    # CI runners always have it, and there a missing `node` must not turn the check off.
    $documentationCheckFailed = $false
    if (Get-Command -Name 'node' -CommandType Application -ErrorAction SilentlyContinue) {
        & node (Join-Path $PSScriptRoot 'update-doc-indexes.mjs') --check
        $documentationCheckFailed = $LASTEXITCODE -ne 0
        # The spec, PARITY.md and DEVIATIONS.md against the documentation standard's checks.
        & node (Join-Path $PSScriptRoot 'check-spec.mjs') --check
        $documentationCheckFailed = $documentationCheckFailed -or $LASTEXITCODE -ne 0
        # The function index spec/index/functions.md, generated from FND-EXE-004 and the entries' citations.
        & node (Join-Path $PSScriptRoot 'spec-coverage.mjs') --check
        $documentationCheckFailed = $documentationCheckFailed -or $LASTEXITCODE -ne 0
    }
    elseif ($env:CI) {
        throw 'Node.js is required to check the generated documentation indexes.'
    }
    else {
        Write-Warning 'Node.js was not found; skipping the generated documentation index check.'
    }

    $msbuildArguments = @(
        "-maxCpuCount:$MaxCpuCount",
        '-nodeReuse:true',
        '--verbosity', 'minimal'
    )
    $isolatedOutputArguments = @("-p:ValidationArtifactsRoot=$validationProjectRoot")
    Invoke-CheckedDotnet -Arguments (@(
        'restore', (Join-Path $repositoryRoot 'Rechaos.slnx')
    ) + $msbuildArguments + $isolatedOutputArguments)
    Invoke-CheckedDotnet -Arguments (@(
            'build', (Join-Path $repositoryRoot 'Rechaos.slnx'),
            '--configuration', 'Release',
            '--no-restore',
            '-p:IncludeOriginalAssets=false'
    ) + $msbuildArguments + $isolatedOutputArguments)
    $testArguments = @(
        'test',
        '--project', (Join-Path $repositoryRoot 'tests/Rechaos.Tests/Rechaos.Tests.csproj'),
        '--configuration', 'Release',
        '--no-build',
        '--no-restore',
        '--no-progress',
        '--timeout', '30m',
        '--verbosity', 'minimal',
        "-p:ValidationArtifactsRoot=$validationProjectRoot"
    )
    if ($TestFilter) {
        $testArguments += @('--filter', $TestFilter)
    }
    elseif ($LongRunningTestsOnly) {
        $testArguments += @(
            '--filter', 'Category=LongRunning',
            '--minimum-expected-tests', '52'
        )
    }
    elseif ($IncludeLongRunningTests) {
        $testArguments += @('--minimum-expected-tests', '1575')
    }
    else {
        $testArguments += @(
            '--filter', 'Category!=LongRunning',
            '--minimum-expected-tests', '1522'
        )
    }
    if ($TraceTestOutput) {
        $testArguments += @(
            '--output', 'Detailed',
            '--show-live-output', 'on',
            '--show-stdout', 'All'
        )
    }
    Invoke-CheckedDotnet -Arguments $testArguments

    if ($documentationCheckFailed) {
        throw 'Documentation check failed; run node tools/update-doc-indexes.mjs, node tools/check-spec.mjs and node tools/spec-coverage.mjs, and fix what they report.'
    }
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

    if (Test-Path -LiteralPath $validationBuildRoot) {
        Remove-Item -LiteralPath $validationBuildRoot -Recurse -Force
    }
}
