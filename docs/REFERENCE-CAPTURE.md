# Original runtime reference capture

The original Windows executable is operated manually. The capture helper does
not inject input, hook the process, or treat its unstable rendering on modern
Windows as a pixel-parity oracle. It records short bursts of the visible client
area so stable gameplay values can be distinguished from transient graphical
corruption.

For the original game's exclusive compatibility mode, start the helper before
launching the game. Open PowerShell as Administrator and run:

```powershell
cd C:\sources\rechaos-overlords
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\Capture-OriginalWindow.ps1 `
  -Experiment initial-city -HotKey -HotKeyTimeoutMinutes 30
```

Leave that PowerShell window running, launch Chaos Overlords, and do not return
to PowerShell or chat until the prepared experiment sequence is complete. At
each requested stable state, press `Ctrl+Shift+F12` once and wait for the
acknowledgement before continuing:

- a rising two-tone sound means the complete frame burst and metadata were
  written successfully;
- a single low tone means capture failed.

The sounds do not draw over the game or change focus. Disable them when testing
original audio behavior:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\Capture-OriginalWindow.ps1 `
  -Experiment audio-test -HotKey -Acknowledgement None
```

The listener runs for 15 minutes by default. Change that bound with
`-HotKeyTimeoutMinutes`. Hotkey checkpoints receive sequential labels and do
not require the helper's console to become active. Exit the game before
returning to PowerShell, then press `Ctrl+C` to stop the listener. Report the
checkpoint meanings afterward, for example:

```text
hotkey-001 = untouched default setup
hotkey-002 = initial city
```

Each checkpoint is written below the ignored
`artifacts/reference-captures/<experiment>/` directory with raw PNG frames,
SHA-256 hashes, timestamps, target-window geometry, acknowledgement mode, and
an append-only local index.

If global hotkeys are isolated from the helper process, launch a hidden delayed
one-shot and immediately return focus to the game:

```powershell
Start-Process powershell.exe -WindowStyle Hidden -ArgumentList @(
    '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File',
    "$PWD/tools/Capture-OriginalWindow.ps1",
    '-Experiment', 'initial-city', '-Once', '-DelaySeconds', '30')
```

Do not switch back to the terminal until the delay and capture burst have
finished. The original can lose its exclusive DirectDraw surface merely from a
foreground transition; that environmental failure is not gameplay evidence.

If automatic discovery does not find the executable, list visible windows:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\Capture-OriginalWindow.ps1 -ListWindows
```

Then pass the reported process and a distinctive title fragment with
`-ProcessName` and `-WindowTitle`. A single non-interactive checkpoint is also
available with `-Once`.

For a windowed program that tolerates focus changes, omit `-HotKey` and enter a
checkpoint label at the `capture:` prompt; enter `q` to stop. This interactive
mode is normally unsuitable for Chaos Overlords because focusing the terminal
can produce a DirectDraw exclusive-mode error.

## Evidence rules

- Preserve all raw burst frames. Do not replace them with a composited or
  cosmetically repaired image.
- Treat a value as observable only when it is stable across the burst or is
  independently corroborated by a save-state delta.
- Record known visual corruption as an environmental artifact, not an original
  rendering fact or a recreation defect.
- Do not commit screenshots, native saves, or other proprietary runtime output.
  Commit only sanitized mechanical fixtures and non-expressive metadata allowed
  by the repository policy.
- Keep the game visible: desktop-copy capture records whatever is actually over
  the client rectangle. This fallback is deliberate because old DirectDraw
  applications often return black or stale content through window-only capture
  APIs.
