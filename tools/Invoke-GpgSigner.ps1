# Signs a release file with a detached, armored OpenPGP signature and verifies the result against
# the key fingerprint the release is expected to carry.
#
# -TestConfiguration checks the signing configuration alone -- the secrets are present, gpg is
# available, and the fingerprint is a fingerprint -- so a release fails on a misconfigured key
# before it spends a build producing artifacts it would have to throw away.
[CmdletBinding(DefaultParameterSetName = 'Sign')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Sign')]
    [string] $InputFile,

    [Parameter(ParameterSetName = 'Sign')]
    [string] $SignaturePath,

    [Parameter(Mandatory, ParameterSetName = 'TestConfiguration')]
    [switch] $TestConfiguration
)

$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 leaves $IsWindows undefined, where -not $IsWindows would otherwise send a
# local run down the POSIX branch. tools/Capture-OriginalWindow.ps1 guards the same variable.
$isWindowsHost = $PSVersionTable.PSEdition -ne 'Core' -or $IsWindows

# Both entry points read the configuration the same way, so a misconfigured key is rejected
# identically whether the release is about to sign something or only checking that it could.
function Resolve-ExpectedFingerprint {
    $required = @('GPG_PRIVATE_KEY', 'GPG_PASSPHRASE', 'GPG_FINGERPRINT')
    $missing = @($required | Where-Object { -not [Environment]::GetEnvironmentVariable($_) })
    if ($missing.Count -ne 0) {
        throw "Missing OpenPGP environment variable(s): $($missing -join ', ')."
    }
    if (-not (Get-Command gpg -ErrorAction SilentlyContinue)) {
        throw 'gpg was not found on PATH.'
    }

    $normalized = (($env:GPG_FINGERPRINT -replace '\s', '') -replace '^0[xX]', '').ToUpperInvariant()
    if ($normalized -notmatch '^[0-9A-F]{40}$') {
        throw ('GPG_FINGERPRINT must be a 40-character OpenPGP key fingerprint, not a short or ' +
            'long key id.')
    }

    return $normalized
}

# Dumping gpg's status output is what makes a rejected signature diagnosable from a release log.
function Write-GpgStatus {
    param([string[]] $Status)

    $Status | ForEach-Object { Write-Host $_ }
}

function Select-GpgStatusLine {
    param(
        [string[]] $Status,

        [Parameter(Mandatory)]
        [string] $Keyword
    )

    $pattern = '^\[GNUPG:\] ' + [Regex]::Escape($Keyword) + '(\s|$)'
    return @($Status | Where-Object { $_ -match $pattern })
}

# Deleting the throwaway home is what keeps the imported secret key off the runner's disk, so the
# delete is retried -- gpg-agent can hold a handle briefly after gpgconf --kill returns -- and the
# reason it could not be deleted is returned rather than discarded. Returns $null on success.
function Remove-EphemeralGnupgHome {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $attempts = 5
    $reason = $null
    for ($attempt = 1; $attempt -le $attempts; $attempt++) {
        if (-not (Test-Path -LiteralPath $Path)) {
            return $null
        }
        try {
            Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
            if (-not (Test-Path -LiteralPath $Path)) {
                return $null
            }
            $reason = 'the directory still exists after it was removed.'
        }
        catch {
            $reason = $_.Exception.Message
        }
        if ($attempt -lt $attempts) {
            Start-Sleep -Milliseconds (200 * $attempt)
        }
    }

    return $reason
}

$expectedFingerprint = Resolve-ExpectedFingerprint

if ($TestConfiguration) {
    Write-Host "OpenPGP signing configuration names key $expectedFingerprint."
    return
}

$resolvedInput = [IO.Path]::GetFullPath($InputFile)
if (-not (Test-Path -LiteralPath $resolvedInput -PathType Leaf)) {
    throw "File to sign was not found at '$resolvedInput'."
}
if (-not $SignaturePath) {
    $SignaturePath = "$resolvedInput.asc"
}
$resolvedSignature = [IO.Path]::GetFullPath($SignaturePath)
if (Test-Path -LiteralPath $resolvedSignature) {
    Remove-Item -LiteralPath $resolvedSignature -Force
}

# The signing key never touches the runner's own keyring; it lives in a throwaway home that the
# finally block kills the agent for and deletes.
$previousGnupgHome = $env:GNUPGHOME
$gnupgHome = Join-Path ([IO.Path]::GetTempPath()) ('gnupg-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $gnupgHome -Force | Out-Null

$cleanupFailure = $null
try {
    if (-not $isWindowsHost) {
        # gpg refuses to treat a home it considers world-readable as safe.
        & chmod 700 $gnupgHome
        if ($LASTEXITCODE -ne 0) { throw "Could not restrict permissions on '$gnupgHome'." }
    }
    $env:GNUPGHOME = $gnupgHome

    $keyPath = Join-Path $gnupgHome 'signing-key.asc'
    [IO.File]::WriteAllText($keyPath, $env:GPG_PRIVATE_KEY, [Text.UTF8Encoding]::new($false))
    try {
        & gpg --batch --quiet --import $keyPath
        if ($LASTEXITCODE -ne 0) { throw 'Could not import the OpenPGP signing key.' }
    }
    finally {
        Remove-Item -LiteralPath $keyPath -Force
    }

    $keyListing = @(& gpg --batch --with-colons --list-secret-keys)
    if ($LASTEXITCODE -ne 0) { throw 'Could not list the imported OpenPGP secret keys.' }
    $importedFingerprints = @($keyListing |
        Where-Object { $_.StartsWith('fpr:', [StringComparison]::Ordinal) } |
        ForEach-Object { ($_ -split ':')[9] })
    if ($importedFingerprints -notcontains $expectedFingerprint) {
        throw ("GPG_PRIVATE_KEY does not hold secret key $expectedFingerprint " +
            "(imported: $($importedFingerprints -join ', ')).")
    }

    $env:GPG_PASSPHRASE | & gpg `
        '--batch' '--yes' '--quiet' `
        '--pinentry-mode' 'loopback' `
        '--passphrase-fd' '0' `
        '--local-user' $expectedFingerprint `
        '--digest-algo' 'SHA512' `
        '--detach-sign' '--armor' `
        '--output' $resolvedSignature `
        $resolvedInput
    if ($LASTEXITCODE -ne 0) { throw "Could not sign '$resolvedInput'." }
    if (-not (Test-Path -LiteralPath $resolvedSignature -PathType Leaf)) {
        throw "gpg reported success but wrote no signature to '$resolvedSignature'."
    }

    # Verifying through the status interface rather than the human-readable output keeps the check
    # from passing on a signature made by some other key that happens to be in the secret.
    $status = @(& gpg --batch --status-fd 1 --verify $resolvedSignature $resolvedInput 2>&1 |
        ForEach-Object { $_.ToString() })
    if ($LASTEXITCODE -ne 0) {
        Write-GpgStatus $status
        throw "Detached signature '$resolvedSignature' did not verify."
    }
    $validSignature = (Select-GpgStatusLine -Status $status -Keyword 'VALIDSIG') |
        Select-Object -First 1
    if (-not $validSignature) {
        Write-GpgStatus $status
        throw "gpg reported no valid signature for '$resolvedInput'."
    }

    # VALIDSIG says the signature is arithmetically sound, which a revoked or expired key still
    # produces; gpg reports one of these instead of GOODSIG and exits 0 either way. Naming them
    # separately turns a key nobody should accept into a release log that says why.
    $rejectedStatuses = [ordered]@{
        REVKEYSIG = 'the signing key has been revoked'
        EXPKEYSIG = 'the signing key has expired'
        EXPSIG    = 'the signature has expired'
    }
    foreach ($rejected in $rejectedStatuses.GetEnumerator()) {
        if (@(Select-GpgStatusLine -Status $status -Keyword $rejected.Key).Count -ne 0) {
            Write-GpgStatus $status
            throw "Refusing to publish '$resolvedSignature': $($rejected.Value)."
        }
    }
    # Requiring GOODSIG outright also covers any further status gpg grows for a key it declines to
    # vouch for, rather than only the three named above.
    if (@(Select-GpgStatusLine -Status $status -Keyword 'GOODSIG').Count -eq 0) {
        Write-GpgStatus $status
        throw "gpg did not report a good signature for '$resolvedInput'."
    }

    # VALIDSIG names the signing key first and the primary key last, so a signing subkey and a
    # signing primary key both satisfy the expected fingerprint.
    $signatureFields = $validSignature.Trim() -split '\s+'
    $signingFingerprints = @($signatureFields[2], $signatureFields[-1])
    if ($signingFingerprints -notcontains $expectedFingerprint) {
        throw ("'$resolvedSignature' was made by $($signatureFields[2]), " +
            "not by the expected key $expectedFingerprint.")
    }

    Write-Host "Detached OpenPGP signature written to $resolvedSignature"
}
finally {
    try { & gpgconf --kill all *> $null } catch { }
    [Environment]::SetEnvironmentVariable('GNUPGHOME', $previousGnupgHome)
    $cleanupFailure = Remove-EphemeralGnupgHome -Path $gnupgHome
    if ($cleanupFailure) {
        Write-Warning ("The throwaway OpenPGP home '$gnupgHome' could not be deleted and may " +
            "still hold the imported signing key: $cleanupFailure")
    }
}

# A home that outlives the job leaves the release signing key on the runner's disk, which on a
# reused runner is the whole point of the throwaway home. Raised after the finally block so it
# reports a leak without masking the signing failure that may have caused it.
if ($cleanupFailure) {
    throw ("Could not delete the throwaway OpenPGP home '$gnupgHome', which may still hold the " +
        "imported signing key: $cleanupFailure")
}
