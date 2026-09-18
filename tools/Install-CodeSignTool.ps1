[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $DestinationPath
)

$ErrorActionPreference = 'Stop'

$version = '1.3.2'
$expectedSha256 = '4AFC32E8B7F79BBE1DE7E4E7049AAAD4E0F754357613B9BBEC0E3052F06FD36B'
$archiveName = "CodeSignTool-v$version-windows.zip"
$downloadUri = "https://github.com/SSLcom/CodeSignTool/releases/download/v$version/$archiveName"
$archivePath = Join-Path ([IO.Path]::GetTempPath()) $archiveName
$resolvedDestination = [IO.Path]::GetFullPath($DestinationPath)

Invoke-WebRequest -Uri $downloadUri -OutFile $archivePath

$actualSha256 = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
if ($actualSha256 -ne $expectedSha256) {
    throw "Unexpected SHA-256 for $archiveName. Expected $expectedSha256, got $actualSha256."
}

New-Item -ItemType Directory -Path $resolvedDestination -Force | Out-Null
Expand-Archive -LiteralPath $archivePath -DestinationPath $resolvedDestination -Force

$requiredFiles = @(
    'jdk-11.0.2/bin/java.exe'
    'jar/code_sign_tool-1.3.2.jar'
)
foreach ($relativePath in $requiredFiles) {
    $requiredPath = Join-Path $resolvedDestination $relativePath
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "CodeSignTool archive did not contain '$relativePath'."
    }
}

$resolvedDestination
