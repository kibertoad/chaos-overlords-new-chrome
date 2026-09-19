# Signs a release file with a detached, armored OpenPGP signature and verifies the result against
# the key fingerprint the release is expected to carry.
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $InputFile,

    [string] $SignaturePath
)

$ErrorActionPreference = 'Stop'

$requiredVariables = @('GPG_PRIVATE_KEY', 'GPG_PASSPHRASE', 'GPG_FINGERPRINT')
$missing = $requiredVariables | Where-Object { -not [Environment]::GetEnvironmentVariable($_) }
if ($missing.Count -ne 0) {
    throw "Missing OpenPGP environment variable(s): $($missing -join ', ')."
}
if (-not (Get-Command gpg -ErrorAction SilentlyContinue)) {
    throw 'gpg was not found on PATH.'
}

$normalizedFingerprint = ($env:GPG_FINGERPRINT -replace '\s', '') -replace '^0[xX]', ''
$expectedFingerprint = $normalizedFingerprint.ToUpperInvariant()
if ($expectedFingerprint -notmatch '^[0-9A-F]{40}$') {
    throw 'GPG_FINGERPRINT must be a 40-character OpenPGP key fingerprint.'
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
if (-not $IsWindows) {
    # gpg refuses to treat a home it considers world-readable as safe.
    & chmod 700 $gnupgHome
    if ($LASTEXITCODE -ne 0) { throw "Could not restrict permissions on '$gnupgHome'." }
}
$env:GNUPGHOME = $gnupgHome

try {
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
        $status | ForEach-Object { Write-Host $_ }
        throw "Detached signature '$resolvedSignature' did not verify."
    }
    $validSignature = @($status |
        Where-Object { $_ -match '^\[GNUPG:\] VALIDSIG\s' }) | Select-Object -First 1
    if (-not $validSignature) {
        $status | ForEach-Object { Write-Host $_ }
        throw "gpg reported no valid signature for '$resolvedInput'."
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
    if (Test-Path -LiteralPath $gnupgHome) {
        Remove-Item -LiteralPath $gnupgHome -Recurse -Force -ErrorAction SilentlyContinue
    }
}
