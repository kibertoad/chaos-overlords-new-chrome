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
# Windows PowerShell 5.1 has no $IsWindows, and runs only on Windows.
$isWindowsHost = ($PSVersionTable.PSEdition -eq 'Desktop') -or $IsWindows
$sha256 = [Security.Cryptography.SHA256]::Create()
try {
    # -TestFilter combines with -IncludeLongRunningTests (the filter then may reach long-running
    # tests); -LongRunningTestsOnly takes neither.
    if ($LongRunningTestsOnly -and ($TestFilter -or $IncludeLongRunningTests)) {
        throw '-LongRunningTestsOnly cannot be combined with -TestFilter or -IncludeLongRunningTests.'
    }

    # The lock and build roots are keyed by the checkout's path. Windows and macOS file systems are
    # case-insensitive by default, so two spellings of one checkout must share a lock there; on
    # Linux they are two checkouts, and folding them together would make one refuse to run while
    # the other validates.
    $caseInsensitivePaths = $isWindowsHost -or $IsMacOS
    $identityPath = if ($caseInsensitivePaths) { $repositoryRoot.ToUpperInvariant() } else { $repositoryRoot }
    $repositoryHash = $sha256.ComputeHash([Text.Encoding]::UTF8.GetBytes($identityPath))
}
finally {
    $sha256.Dispose()
}
$repositoryIdentity = [BitConverter]::ToString($repositoryHash).Replace('-', '')
$lockPath = Join-Path $temporaryRoot "rechaos-validation-$($repositoryIdentity.Substring(0, 16)).lock"
$validationBuildRoot = Join-Path $temporaryRoot (
    "rechaos-validation-$($repositoryIdentity.Substring(0, 16))-$([Guid]::NewGuid().ToString('N'))")
$lock = $null

# A batch/command shim on PATH may reinterpret test filters containing & or |.
# Honour DOTNET_ROOT when it contains the native host; otherwise resolve that host
# explicitly on Windows rather than a dotnet.cmd wrapper. Unix keeps dotnet's name.
# A DOTNET_ROOT that cannot be probed (a missing drive, which Windows PowerShell's Join-Path
# rejects, or characters a path cannot hold) falls through to PATH instead of aborting. With no
# native host on PATH, the plain command name is kept as before, so an unfiltered run still works
# through a shim; only compound filters are at risk there.
$dotnetHostName = if ($isWindowsHost) { 'dotnet.exe' } else { 'dotnet' }
$dotnetRootHost = $null
if ($env:DOTNET_ROOT) {
    try {
        $candidate = [IO.Path]::Combine($env:DOTNET_ROOT, $dotnetHostName)
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { $dotnetRootHost = $candidate }
    }
    catch {
        Write-Warning "DOTNET_ROOT could not be probed for $dotnetHostName; resolving the host from PATH."
    }
}
$dotnetExecutable = if ($dotnetRootHost) {
    $dotnetRootHost
} else {
    $nativeHost = Get-Command -Name $dotnetHostName -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($nativeHost) {
        $nativeHost.Source
    } else {
        Write-Warning "No native $dotnetHostName was found on PATH; test filters containing & or | may not reach the runner intact."
        'dotnet'
    }
}

function Invoke-CheckedDotnet {
    param([Parameter(Mandatory = $true)][string[]] $Arguments)

    & $dotnetExecutable @Arguments
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
    # Minimum counts are what `dotnet test --list-tests` discovers with the same filters, so a filter
    # or discovery change that silently drops tests fails the run; theories whose rows are only
    # expanded at run time make the executed count somewhat higher. Full = fast + long-running;
    # raise all three together when tests are added.
    $minimumFastTests = 3060
    $minimumLongRunningTests = 53
    $minimumAllTests = $minimumFastTests + $minimumLongRunningTests
    if ($TestFilter) {
        # A narrowed run still honours the fast-gate scope: the long-running campaigns stay out
        # unless asked for, and a filter that matches nothing fails instead of passing vacuously.
        $filter = if ($IncludeLongRunningTests) { $TestFilter } else { "($TestFilter)&Category!=LongRunning" }
        $testArguments += @(
            '--filter', $filter,
            '--minimum-expected-tests', '1'
        )
    }
    elseif ($LongRunningTestsOnly) {
        $testArguments += @(
            '--filter', 'Category=LongRunning',
            '--minimum-expected-tests', "$minimumLongRunningTests"
        )
    }
    elseif ($IncludeLongRunningTests) {
        $testArguments += @('--minimum-expected-tests', "$minimumAllTests")
    }
    else {
        $testArguments += @(
            '--filter', 'Category!=LongRunning',
            '--minimum-expected-tests', "$minimumFastTests"
        )
    }
    if ($TraceTestOutput) {
        $testArguments += @(
            '--output', 'Detailed',
            '--show-live-output', 'on',
            '--show-stdout', 'All'
        )
    }
    # Native soundtrack regression tests use synthesized silence and OpenAL's null driver.
    # Set this in the parent before dotnet starts: on Unix, a managed runtime environment
    # change does not update the native environment that OpenAL reads.
    $previousAudioDriver = $env:ALSOFT_DRIVERS
    try {
        $env:ALSOFT_DRIVERS = 'null'
        Invoke-CheckedDotnet -Arguments $testArguments
    }
    finally {
        if ($null -eq $previousAudioDriver) {
            Remove-Item Env:ALSOFT_DRIVERS -ErrorAction SilentlyContinue
        }
        else {
            $env:ALSOFT_DRIVERS = $previousAudioDriver
        }
    }

    if ($documentationCheckFailed) {
        throw 'Documentation check failed; run node tools/update-doc-indexes.mjs, node tools/check-spec.mjs and node tools/spec-coverage.mjs, and fix what they report.'
    }
}
finally {
    if ($ShutdownBuildServersAfterRun) {
        Write-Host 'Stopping .NET build servers for the current user.'
        & $dotnetExecutable build-server shutdown
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
