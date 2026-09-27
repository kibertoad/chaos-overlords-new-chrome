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
# AGENTS.md: decompiler output, disassembly listings and analysis databases never go into the
# repository, wherever they sit — there is no approved root for them. Ghidra projects are a `.gpr`
# file, a `.rep` directory and `.lock`/`.lock~` files; packed programs and archives are `.gzf`,
# `.gar` and `.gdt`; IDA databases are `.idb`/`.i64` (or unpacked `.id0`-`.id2`/`.nam`/`.til`);
# Binary Ninja's are `.bndb`; listings are `.lst` and `.asm`. The rebuild has no hand-written
# assembly, so an `.asm` here is a listing. A `.lock` is not: package managers and toolchains write
# `yarn.lock`, `Cargo.lock` and the like, so only a Ghidra project's own lock — `<project>.lock`
# beside that project's `.gpr` or `.rep` — counts.
$analysisExtensions = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($extension in $policy.analysisArtifactExtensions) {
    [void] $analysisExtensions.Add([string] $extension)
}
$analysisDirectorySuffixes = @($policy.analysisArtifactDirectorySuffixes | ForEach-Object { [string] $_ })
$projectLockExtensions = @($policy.analysisProjectLockExtensions | ForEach-Object { [string] $_ })
$projectExtensions = @($policy.analysisProjectExtensions | ForEach-Object { [string] $_ })
$trackedPathSet = [Collections.Generic.HashSet[string]]::new(
    [string[]] $trackedPaths, [StringComparer]::OrdinalIgnoreCase)

# A project lock is recognised by its project, tracked or merely present in the worktree: the lock
# is what gets committed by accident while the project itself is ignored.
function Test-AnalysisProjectLock([string] $Path) {
    $name = [IO.Path]::GetFileName($Path)
    foreach ($lockExtension in $projectLockExtensions) {
        if (-not $name.EndsWith($lockExtension, [StringComparison]::OrdinalIgnoreCase)) { continue }
        $stem = $Path.Substring(0, $Path.Length - $lockExtension.Length)
        foreach ($projectExtension in $projectExtensions) {
            $project = $stem + $projectExtension
            if ($trackedPathSet.Contains($project)) { return $true }
            $absoluteProject = Join-Path $root $project
            if ([IO.File]::Exists($absoluteProject) -or [IO.Directory]::Exists($absoluteProject)) {
                return $true
            }
        }
    }

    return $false
}

function Test-AnalysisArtifact([string] $Path) {
    if ($analysisExtensions.Contains([IO.Path]::GetExtension($Path))) {
        return $true
    }

    if (Test-AnalysisProjectLock $Path) {
        return $true
    }

    $segments = $Path.Split('/')
    for ($index = 0; $index -lt $segments.Length - 1; $index++) {
        foreach ($suffix in $analysisDirectorySuffixes) {
            if ($segments[$index].EndsWith($suffix, [StringComparison]::OrdinalIgnoreCase)) {
                return $true
            }
        }
    }

    return $false
}

foreach ($path in $trackedPaths) {
    if (Starts-WithRepositoryRoot $path $policy.deniedRoots) {
        $violations.Add("tracked local/imported content: $path")
        continue
    }

    if (Test-AnalysisArtifact $path) {
        $violations.Add("decompiler, disassembly or analysis-database artifact: $path")
        continue
    }

    $extension = [IO.Path]::GetExtension($path)
    $approvedRestrictedPath = Starts-WithRepositoryRoot $path $policy.approvedRestrictedRoots
    if ($restrictedExtensions.Contains($extension) -and -not $approvedRestrictedPath) {
        $violations.Add("restricted media outside an approved clean-room/synthetic root: $path")
    }

    $absolutePath = Join-Path $root $path
    if (-not [IO.File]::Exists($absolutePath)) {
        $violations.Add("tracked path is missing from the worktree: $path")
        continue
    }

    $length = [IO.FileInfo]::new($absolutePath).Length
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
