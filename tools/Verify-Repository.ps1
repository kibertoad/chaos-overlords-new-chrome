[CmdletBinding()]
param(
    [string] $RepositoryRoot
)

$ErrorActionPreference = 'Stop'

if (-not $RepositoryRoot) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}

function Normalize-RepositoryPath([string] $Path) {
    $normalized = $Path.Replace('\', '/')
    if ($normalized.StartsWith('./', [StringComparison]::Ordinal)) {
        return $normalized.Substring(2)
    }

    return $normalized
}

function Starts-WithRepositoryRoot([string] $Path, [string[]] $Roots) {
    foreach ($candidateRoot in $Roots) {
        if ($Path.StartsWith($candidateRoot, [StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$safeRoot = $root.Replace('\', '/')
$policyPath = Join-Path $PSScriptRoot 'repository-policy.json'
$policy = Get-Content -LiteralPath $policyPath -Raw | ConvertFrom-Json

$trackedOutput = @(& git -c "safe.directory=$safeRoot" -c core.quotepath=false -C $root ls-files)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to enumerate tracked files under '$root'."
}

$trackedPaths = @($trackedOutput | Where-Object { $_ } |
    ForEach-Object { Normalize-RepositoryPath $_ })
$violations = [Collections.Generic.List[string]]::new()
$restrictedExtensions = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($extension in $policy.restrictedExtensions) {
    [void] $restrictedExtensions.Add([string] $extension)
}

foreach ($path in $trackedPaths) {
    if (Starts-WithRepositoryRoot $path $policy.deniedRoots) {
        $violations.Add("tracked local/imported content: $path")
        continue
    }

    $extension = [IO.Path]::GetExtension($path)
    $approvedRestrictedPath = Starts-WithRepositoryRoot $path $policy.approvedRestrictedRoots
    if ($restrictedExtensions.Contains($extension) -and -not $approvedRestrictedPath) {
        $violations.Add("restricted media outside an approved clean-room/synthetic root: $path")
    }

    $absolutePath = Join-Path $root $path
    if (-not (Test-Path -LiteralPath $absolutePath -PathType Leaf)) {
        $violations.Add("tracked path is missing from the worktree: $path")
        continue
    }

    $length = (Get-Item -LiteralPath $absolutePath).Length
    $approvedLargeFile = $policy.approvedLargeFiles -contains $path
    if ($length -gt $policy.maximumTrackedFileBytes -and -not $approvedLargeFile) {
        $violations.Add("unreviewed large file ($length bytes): $path")
    }
}

if ($violations.Count -gt 0) {
    Write-Error ("Repository policy failed:`n - " + ($violations -join "`n - "))
    exit 1
}

Write-Host "Repository policy passed for $($trackedPaths.Count) tracked files."
