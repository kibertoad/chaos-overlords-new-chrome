---
id: FND-PLATFORM-003
title: Save files are chosen with the common file dialogs and read and written through thin Win32 wrappers
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AE968..0x004AE970
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042B27A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042AFDD
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042AC7A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042ADE9
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042AE85
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042AF31
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487644..0x004876AA
tool: Ghidra 12.1.3
environment: null
---

## Observation

- `KERNEL32.dll` imports include `CreateFileA`, `ReadFile`, `WriteFile`,
  `GetFileSize`, `SetFilePointer` and `FlushFileBuffers`. `comdlg32.dll`
  supplies `GetSaveFileNameA` and `GetOpenFileNameA` at `0x004AE968` and
  `0x004AE96C`.
- The data at `0x00487644..0x004876AA` holds the file-type filter strings for
  save files, whose pattern is `*.SAV`, and the prompt for a save name.
- Save dialog function `0x0042B27A` creates or truncates the chosen file.
  Open dialog function `0x0042AFDD` chooses an existing file.
- `0x0042AC7A` opens the file for the current slot, and falls back to read-only
  access when read and write access fails. `0x0042ADE9` closes it.
- `0x0042AE85` and `0x0042AF31` wrap `ReadFile` and `WriteFile` and return the
  byte count each call reports.

## Interpretation

The player names save files in the standard Windows dialogs, with the
extension `.SAV`. What is written to them is described in FND-SAVE-001.

## Alternatives

None known.

## How to reproduce

Follow the callers of `GetSaveFileNameA` and `GetOpenFileNameA` through their
import slots at `0x004AE968` and `0x004AE96C`.
