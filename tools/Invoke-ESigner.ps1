[CmdletBinding(DefaultParameterSetName = 'BatchSign')]
param(
    [Parameter(Mandatory, ParameterSetName = 'BatchSign')]
    [string] $InputDirectory,

    [Parameter(Mandatory, ParameterSetName = 'BatchSign')]
    [string] $OutputDirectory,

    [Parameter(Mandatory, ParameterSetName = 'Sign')]
    [string] $InputFile,

    [Parameter(Mandatory)]
    [string] $CodeSignToolPath
)

$ErrorActionPreference = 'Stop'

$requiredVariables = @('ES_USERNAME', 'ES_PASSWORD', 'CREDENTIAL_ID', 'ES_TOTP_SECRET')
$missing = $requiredVariables | Where-Object { -not [Environment]::GetEnvironmentVariable($_) }
if ($missing.Count -ne 0) {
    throw "Missing eSigner environment variable(s): $($missing -join ', ')."
}

$toolRoot = [IO.Path]::GetFullPath($CodeSignToolPath)
$java = Join-Path $toolRoot 'jdk-11.0.2/bin/java.exe'
$jar = Join-Path $toolRoot 'jar/code_sign_tool-1.3.2.jar'
if (-not (Test-Path -LiteralPath $java -PathType Leaf)) {
    throw "Bundled CodeSignTool Java runtime was not found at '$java'."
}
if (-not (Test-Path -LiteralPath $jar -PathType Leaf)) {
    throw "CodeSignTool jar was not found at '$jar'."
}

function Invoke-CodeSignTool {
    param(
        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    Push-Location -LiteralPath $toolRoot
    try {
        $output = @(& $java '-Xmx1024M' '-jar' $jar @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    $outputText = @($output | ForEach-Object { $_.ToString() })
    $outputText | ForEach-Object { Write-Host $_ }

    $reportedFailure = $outputText | Where-Object {
        $_ -match '(?i)\b(error|exception)\b|missing required option|unmatched argument'
    }
    if ($exitCode -ne 0 -or $reportedFailure) {
        throw "CodeSignTool failed with exit code $exitCode."
    }
}

$authenticationArguments = @(
    "-username=$env:ES_USERNAME"
    "-password=$env:ES_PASSWORD"
    "-credential_id=$env:CREDENTIAL_ID"
)

try {
    if ($PSCmdlet.ParameterSetName -eq 'BatchSign') {
        $resolvedInput = [IO.Path]::GetFullPath($InputDirectory)
        $resolvedOutput = [IO.Path]::GetFullPath($OutputDirectory)
        $files = @(Get-ChildItem -LiteralPath $resolvedInput -File)
        if ($files.Count -eq 0) {
            throw "No files were found to sign in '$resolvedInput'."
        }

        # batch_sign cannot request a scan itself, so submit every hash before signing the batch.
        foreach ($file in $files) {
            Invoke-CodeSignTool -Arguments @(
                'scan_code'
                $authenticationArguments
                "-input_file_path=$($file.FullName)"
            )
        }

        New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null
        Invoke-CodeSignTool -Arguments @(
            'batch_sign'
            $authenticationArguments
            "-totp_secret=$env:ES_TOTP_SECRET"
            "-input_dir_path=$resolvedInput"
            "-output_dir_path=$resolvedOutput"
        )
    }
    else {
        $resolvedInput = [IO.Path]::GetFullPath($InputFile)
        if (-not (Test-Path -LiteralPath $resolvedInput -PathType Leaf)) {
            throw "File to sign was not found at '$resolvedInput'."
        }

        Invoke-CodeSignTool -Arguments @(
            'sign'
            $authenticationArguments
            "-totp_secret=$env:ES_TOTP_SECRET"
            "-input_file_path=$resolvedInput"
            '-override=true'
            '-malware_block=true'
        )
    }
}
finally {
    $logsPath = Join-Path $toolRoot 'logs'
    if (Test-Path -LiteralPath $logsPath) {
        Remove-Item -LiteralPath $logsPath -Recurse -Force
    }
}
