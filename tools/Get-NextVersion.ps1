<#
.SYNOPSIS
Prints the version that a release of the given kind would publish.

.DESCRIPTION
A release is asked for by kind, not by number: `version.txt` records the version that was last
released, so the next one follows from it. A major release resets the minor and patch parts, a
minor release resets the patch part, and a patch release counts on. Run this before starting a
release to see what the release will be called.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('major', 'minor', 'patch')]
    [string] $Bump,

    # The version to count from. Defaults to the one recorded in `version.txt`.
    [string] $CurrentVersion,

    [string] $RepositoryRoot
)

$ErrorActionPreference = 'Stop'

if (-not $CurrentVersion) {
    $lookup = @{}
    if ($RepositoryRoot) { $lookup['RepositoryRoot'] = $RepositoryRoot }
    $CurrentVersion = & (Join-Path $PSScriptRoot 'Get-GameVersion.ps1') @lookup
}

if ($CurrentVersion -notmatch '^(\d+)\.(\d+)\.(\d+)$') {
    throw "The version to count from must look like 0.1.0; found '$CurrentVersion'."
}

$major, $minor, $patch = [int] $Matches[1], [int] $Matches[2], [int] $Matches[3]

switch ($Bump) {
    'major' { return "$($major + 1).0.0" }
    'minor' { return "$major.$($minor + 1).0" }
    'patch' { return "$major.$minor.$($patch + 1)" }
}
