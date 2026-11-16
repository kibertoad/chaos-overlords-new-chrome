[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $GameExecutable
)

# Runs a packaged macOS or Linux game with --platform-smoke-test, which constructs the MonoGame
# window and so loads SDL and OpenAL. --smoke-test returns before MonoGame is touched and cannot
# catch a native library the package fails to ship or MonoGame fails to find.
#
# It runs from the root directory, as Finder and desktop launchers do, so a library that is found
# only through the working directory fails here too. A Linux host without a display runs it under
# xvfb-run, since SDL cannot initialize video without an X server.

$ErrorActionPreference = 'Stop'
$executable = (Resolve-Path -LiteralPath $GameExecutable).Path
$command = @($executable, '--platform-smoke-test')
if ($IsLinux -and -not $env:DISPLAY -and -not $env:WAYLAND_DISPLAY) {
    if (-not (Get-Command xvfb-run -ErrorAction SilentlyContinue)) {
        throw 'No display is available and xvfb-run is not installed; the platform smoke test needs one.'
    }
    $command = @('xvfb-run', '--auto-servernum') + $command
}

Push-Location -LiteralPath '/'
try {
    & $command[0] @($command | Select-Object -Skip 1)
    $exitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}
if ($exitCode -ne 0) {
    [Console]::Error.WriteLine("Platform smoke test of '$executable' exited with $exitCode.")
    exit $exitCode
}
exit 0
