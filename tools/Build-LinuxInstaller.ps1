[CmdletBinding()]
param(
    [string] $Version = '0.1.0'
)

$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Invalid installer version '$Version'; expected x.y.z."
}

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
$portableRoot = Join-Path $artifactsRoot 'ChaosOverlordsNewChrome-linux-x64'
& (Join-Path $PSScriptRoot 'Publish-Portable.ps1') -Runtime linux-x64 `
    -OutputDirectory $portableRoot
if ($LASTEXITCODE -ne 0) { throw 'Linux package creation failed.' }

$stagingRoot = Join-Path $artifactsRoot ".linux-deb-$Version"
$resolvedStaging = [IO.Path]::GetFullPath($stagingRoot)
$artifactsPrefix = $artifactsRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) +
    [IO.Path]::DirectorySeparatorChar
if (-not $resolvedStaging.StartsWith($artifactsPrefix, [StringComparison]::Ordinal)) {
    throw "Staging output must remain below '$artifactsRoot'."
}
if (Test-Path -LiteralPath $resolvedStaging) {
    Remove-Item -LiteralPath $resolvedStaging -Recurse -Force
}

$installRoot = Join-Path $resolvedStaging 'opt/chaos-overlords-new-chrome'
$debianRoot = Join-Path $resolvedStaging 'DEBIAN'
$binaryRoot = Join-Path $resolvedStaging 'usr/bin'
$desktopRoot = Join-Path $resolvedStaging 'usr/share/applications'
New-Item -ItemType Directory -Path $installRoot, $debianRoot, $binaryRoot, $desktopRoot `
    -Force | Out-Null
Get-ChildItem -LiteralPath $portableRoot | Copy-Item -Destination $installRoot -Recurse

@"
Package: chaos-overlords-new-chrome
Version: $Version
Section: games
Priority: optional
Architecture: amd64
Maintainer: kibertoad
Depends: libc6, libgl1, libx11-6
Description: Clean-room recreation of Chaos Overlords
 Requires resources extracted from a legally owned original copy.
"@ | Set-Content -LiteralPath (Join-Path $debianRoot 'control') -Encoding utf8NoBOM

@'
#!/bin/sh
exec /opt/chaos-overlords-new-chrome/Game/Rechaos.Game "$@"
'@ | Set-Content -LiteralPath (Join-Path $binaryRoot 'chaos-overlords-new-chrome') `
    -Encoding utf8NoBOM

@'
#!/bin/sh
set -eu
if [ "$#" -ne 1 ]; then
  echo "Usage: chaos-overlords-new-chrome-import /path/to/ChaosOverlords" >&2
  exit 2
fi
data_root="${XDG_DATA_HOME:-$HOME/.local/share}/ChaosOverlordsNewChrome/Assets"
exec /opt/chaos-overlords-new-chrome/Tools/Rechaos.Extractor \
  --source "$1" --output "$data_root"
'@ | Set-Content -LiteralPath (Join-Path $binaryRoot 'chaos-overlords-new-chrome-import') `
    -Encoding utf8NoBOM

@'
[Desktop Entry]
Type=Application
Name=Chaos Overlords: New Chrome
Comment=Clean-room recreation of Chaos Overlords
Exec=chaos-overlords-new-chrome
Terminal=false
Categories=Game;StrategyGame;
'@ | Set-Content -LiteralPath (Join-Path $desktopRoot 'chaos-overlords-new-chrome.desktop') `
    -Encoding utf8NoBOM

& chmod 755 (Join-Path $binaryRoot 'chaos-overlords-new-chrome') `
    (Join-Path $binaryRoot 'chaos-overlords-new-chrome-import')
if ($LASTEXITCODE -ne 0) { throw 'Could not mark Linux launchers as executable.' }

$installer = Join-Path $artifactsRoot "ChaosOverlords-NewChrome-linux-x64-Setup-$Version.deb"
if (Test-Path -LiteralPath $installer) { Remove-Item -LiteralPath $installer -Force }
& dpkg-deb --build --root-owner-group $resolvedStaging $installer
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $installer -PathType Leaf)) {
    throw 'Debian installer creation failed.'
}
Remove-Item -LiteralPath $resolvedStaging -Recurse -Force
Write-Host "Linux installer created at $installer"
