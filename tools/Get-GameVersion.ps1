<#
.SYNOPSIS
Prints the repository's release version.

.DESCRIPTION
`version.txt` is the one place the version is written down. The build stamps it onto every
assembly, so an installer named after a different number would ship a game that reports the old
one; the installer scripts read it from here instead of carrying their own default.
#>
[CmdletBinding()]
param(
    [string] $RepositoryRoot
)

$ErrorActionPreference = 'Stop'
if (-not $RepositoryRoot) {
    $RepositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
}

$versionFile = Join-Path $RepositoryRoot 'version.txt'
if (-not (Test-Path -LiteralPath $versionFile -PathType Leaf)) {
    throw "The repository version file is missing at '$versionFile'."
}

$version = (Get-Content -LiteralPath $versionFile -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw "'$versionFile' must hold one x.y.z version; found '$version'."
}

return $version
