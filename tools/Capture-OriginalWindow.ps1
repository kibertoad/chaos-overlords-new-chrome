[CmdletBinding()]
param(
    [string] $ProcessName = 'Chaos',
    [string] $WindowTitle = 'Chaos Overlords',
    [string] $Experiment = 'manual',
    [string] $OutputRoot,
    [ValidateRange(1, 30)]
    [int] $BurstCount = 5,
    [ValidateRange(0, 5000)]
    [int] $BurstIntervalMilliseconds = 100,
    [ValidateRange(0, 300)]
    [int] $DelaySeconds = 0,
    [ValidateSet('Sound', 'None')]
    [string] $Acknowledgement = 'Sound',
    [switch] $Once,
    [switch] $HotKey,
    [ValidateRange(1, 1440)]
    [int] $HotKeyTimeoutMinutes = 15,
    [switch] $ListWindows
)

$ErrorActionPreference = 'Stop'

if (-not $IsWindows -and $PSVersionTable.PSEdition -eq 'Core') {
    throw 'Original-window capture is supported only on Windows.'
}

if (-not $OutputRoot) {
    $repositoryRoot = Split-Path -Parent $PSScriptRoot
    $OutputRoot = Join-Path $repositoryRoot 'artifacts\reference-captures'
}

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

if (-not ('Rechaos.ReferenceCapture.NativeMethods' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace Rechaos.ReferenceCapture
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
    }

    public static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetClientRect(IntPtr window, out Rect rect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ClientToScreen(IntPtr window, ref Point point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr window);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int virtualKey);
    }
}
'@
}

function Get-CapturableWindows {
    @(Get-Process -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero -and $_.MainWindowTitle } |
        Sort-Object ProcessName, Id |
        ForEach-Object {
            [pscustomobject]@{
                Id = $_.Id
                ProcessName = $_.ProcessName
                Title = $_.MainWindowTitle
                Handle = $_.MainWindowHandle
            }
        })
}

function Find-TargetWindow {
    $windows = @(Get-CapturableWindows)
    $matches = @($windows | Where-Object {
        ($_.ProcessName -ieq $ProcessName -or $_.ProcessName -like "$ProcessName*") -and
        ($WindowTitle.Length -eq 0 -or $_.Title -like "*$WindowTitle*")
    })

    if ($matches.Count -eq 0) {
        $available = if ($windows.Count -eq 0) {
            'No top-level windows are visible to this process.'
        }
        else {
            ($windows | ForEach-Object { "  PID $($_.Id) $($_.ProcessName): $($_.Title)" }) -join [Environment]::NewLine
        }

        throw "No window matched process '$ProcessName' and title '$WindowTitle'. Available windows:`n$available"
    }

    if ($matches.Count -gt 1) {
        $descriptions = ($matches | ForEach-Object { "PID $($_.Id): $($_.Title)" }) -join ', '
        throw "More than one window matched: $descriptions. Narrow -ProcessName or -WindowTitle."
    }

    $matches[0]
}

function Get-ClientBounds([IntPtr] $Window) {
    $rect = [Rechaos.ReferenceCapture.Rect]::new()
    if (-not [Rechaos.ReferenceCapture.NativeMethods]::GetClientRect($Window, [ref] $rect)) {
        throw "GetClientRect failed with Win32 error $([Runtime.InteropServices.Marshal]::GetLastWin32Error())."
    }

    $origin = [Rechaos.ReferenceCapture.Point]::new()
    if (-not [Rechaos.ReferenceCapture.NativeMethods]::ClientToScreen($Window, [ref] $origin)) {
        throw "ClientToScreen failed with Win32 error $([Runtime.InteropServices.Marshal]::GetLastWin32Error())."
    }

    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) {
        throw "The target client area has invalid dimensions ${width}x${height}."
    }

    [pscustomobject]@{ X = $origin.X; Y = $origin.Y; Width = $width; Height = $height }
}

function ConvertTo-SafeName([string] $Value, [string] $Fallback) {
    $safe = [regex]::Replace($Value.Trim(), '[^A-Za-z0-9._-]+', '-')
    $safe = $safe.Trim('-')
    if ($safe) { return $safe }
    $Fallback
}

function Send-CaptureAcknowledgement([bool] $Succeeded) {
    if ($Acknowledgement -eq 'None') { return }

    try {
        if ($Succeeded) {
            [Console]::Beep(1000, 90)
            [Console]::Beep(1400, 110)
        }
        else {
            [Console]::Beep(350, 300)
        }
    }
    catch {
        # An unavailable system speaker must not change the capture outcome.
    }
}

function Save-ScreenFrame($Bounds, [string] $Path) {
    $bitmap = [Drawing.Bitmap]::new($Bounds.Width, $Bounds.Height, [Drawing.Imaging.PixelFormat]::Format24bppRgb)
    try {
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CopyFromScreen(
                $Bounds.X,
                $Bounds.Y,
                0,
                0,
                [Drawing.Size]::new($Bounds.Width, $Bounds.Height),
                [Drawing.CopyPixelOperation]::SourceCopy)
        }
        finally {
            $graphics.Dispose()
        }

        $bitmap.Save($Path, [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }
}

function New-Checkpoint([string] $Label) {
    $target = Find-TargetWindow
    if ([Rechaos.ReferenceCapture.NativeMethods]::IsIconic($target.Handle)) {
        throw 'The target window is minimized. Restore it before capturing.'
    }

    $bounds = Get-ClientBounds $target.Handle
    $safeExperiment = ConvertTo-SafeName $Experiment 'manual'
    $safeLabel = ConvertTo-SafeName $Label 'checkpoint'
    $experimentDirectory = Join-Path $OutputRoot $safeExperiment
    [IO.Directory]::CreateDirectory($experimentDirectory) | Out-Null

    $capturedAt = [DateTimeOffset]::Now
    $checkpointName = '{0}-{1}' -f $capturedAt.ToString('yyyyMMdd-HHmmss-fff'), $safeLabel
    $checkpointDirectory = Join-Path $experimentDirectory $checkpointName
    [IO.Directory]::CreateDirectory($checkpointDirectory) | Out-Null

    $frames = [Collections.Generic.List[object]]::new()
    for ($index = 1; $index -le $BurstCount; $index++) {
        $fileName = 'frame-{0:D2}.png' -f $index
        $framePath = Join-Path $checkpointDirectory $fileName
        Save-ScreenFrame $bounds $framePath
        $hash = (Get-FileHash -LiteralPath $framePath -Algorithm SHA256).Hash.ToLowerInvariant()
        $frames.Add([ordered]@{
            file = $fileName
            sha256 = $hash
            capturedAt = [DateTimeOffset]::Now.ToString('o')
        })

        if ($index -lt $BurstCount -and $BurstIntervalMilliseconds -gt 0) {
            Start-Sleep -Milliseconds $BurstIntervalMilliseconds
        }
    }

    $virtualScreen = [Windows.Forms.SystemInformation]::VirtualScreen
    $metadata = [ordered]@{
        schemaVersion = 1
        experiment = $Experiment
        label = $Label
        capturedAt = $capturedAt.ToString('o')
        captureMethod = 'visible-client-area-desktop-copy'
        acknowledgement = $Acknowledgement.ToLowerInvariant()
        evidencePolicy = 'Raw burst frames may contain modern-OS rendering glitches; use stable repeated state only.'
        target = [ordered]@{
            processId = $target.Id
            processName = $target.ProcessName
            windowTitle = $target.Title
            clientBounds = [ordered]@{
                x = $bounds.X
                y = $bounds.Y
                width = $bounds.Width
                height = $bounds.Height
            }
        }
        virtualScreen = [ordered]@{
            x = $virtualScreen.X
            y = $virtualScreen.Y
            width = $virtualScreen.Width
            height = $virtualScreen.Height
        }
        burst = [ordered]@{
            count = $BurstCount
            intervalMilliseconds = $BurstIntervalMilliseconds
            frames = $frames
        }
    }

    $metadataPath = Join-Path $checkpointDirectory 'checkpoint.json'
    $metadata | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $metadataPath -Encoding utf8
    $indexRecord = [ordered]@{
        capturedAt = $capturedAt.ToString('o')
        experiment = $Experiment
        label = $Label
        directory = $checkpointName
        processId = $target.Id
        frameCount = $BurstCount
    } | ConvertTo-Json -Compress
    Add-Content -LiteralPath (Join-Path $experimentDirectory 'index.jsonl') -Value $indexRecord -Encoding utf8

    Write-Host "Captured '$Label' to $checkpointDirectory"
    Send-CaptureAcknowledgement $true
    $checkpointDirectory
}

if ($ListWindows) {
    Get-CapturableWindows | Format-Table Id, ProcessName, Title -AutoSize
    exit 0
}

if ($Once) {
    if ($DelaySeconds -gt 0) {
        Start-Sleep -Seconds $DelaySeconds
    }
    try {
        New-Checkpoint 'checkpoint' | Out-Null
    }
    catch {
        Send-CaptureAcknowledgement $false
        throw
    }
    exit 0
}

if ($HotKey) {
    $controlKey = 0x11
    $shiftKey = 0x10
    $captureKey = 0x7b
    $deadline = [DateTimeOffset]::Now.AddMinutes($HotKeyTimeoutMinutes)
    $captureNumber = 0
    $wasPressed = $false

    Write-Host "Reference capture hotkey is ready for experiment '$Experiment'."
    Write-Host "Keep the game active and press Ctrl+Shift+F12 to capture. Listener expires at $($deadline.ToString('o'))."
    while ([DateTimeOffset]::Now -lt $deadline) {
        $controlDown = ([Rechaos.ReferenceCapture.NativeMethods]::GetAsyncKeyState($controlKey) -band 0x8000) -ne 0
        $shiftDown = ([Rechaos.ReferenceCapture.NativeMethods]::GetAsyncKeyState($shiftKey) -band 0x8000) -ne 0
        $captureDown = ([Rechaos.ReferenceCapture.NativeMethods]::GetAsyncKeyState($captureKey) -band 0x8000) -ne 0
        $pressed = $controlDown -and $shiftDown -and $captureDown

        if ($pressed -and -not $wasPressed) {
            $captureNumber++
            try {
                New-Checkpoint ('hotkey-{0:D3}' -f $captureNumber) | Out-Null
            }
            catch {
                Send-CaptureAcknowledgement $false
                Write-Warning $_.Exception.Message
            }
        }

        $wasPressed = $pressed
        Start-Sleep -Milliseconds 40
    }

    Write-Host 'Reference capture hotkey listener expired.'
    exit 0
}

Write-Host "Reference capture is ready for experiment '$Experiment'."
Write-Host 'Keep the original game visible and unobscured.'
Write-Host 'Enter a checkpoint label and press Enter. Enter q to stop.'
while ($true) {
    $label = Read-Host 'capture'
    if ($label -eq 'q') { break }
    if (-not $label) { $label = 'checkpoint' }

    try {
        New-Checkpoint $label | Out-Null
    }
    catch {
        Send-CaptureAcknowledgement $false
        Write-Warning $_.Exception.Message
    }
}
