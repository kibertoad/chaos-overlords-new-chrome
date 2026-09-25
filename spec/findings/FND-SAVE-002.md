---
id: FND-SAVE-002
title: Save and Open use the common dialogs on file slot 3, the save dialog truncates the chosen file before anything is written, and nothing writes an M10W file
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042AFDD..0x0042B60E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487640..0x004876B3
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487B3C..0x00487B3E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004637B8..0x00464107
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00458155..0x00458287
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00478630..0x0047863B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00498330..0x0049833B
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges of the functions named here are in FND-EXE-004. The file slots are
described in FND-PLATFORM-010.

`0x00478630` and `0x00478636` are six-byte jumps through the import slots of
`GetOpenFileNameA` and `GetSaveFileNameA`. Each has one caller:
`fn_0042AFDD` and `fn_0042B27A`. Both fill the `OPENFILENAMEA` inside the
record of the slot they are given (always slot 3) and differ only as the table
shows:

| Field | Open, `fn_0042AFDD` | Save, `fn_0042B27A` |
|---|---|---|
| `lStructSize` | `0x4C` | `0x4C` |
| `hwndOwner`, `hInstance` | `0x00493664`, `0x00487B80` | the same |
| `lpstrFilter` | a stack copy of the 26 bytes at `0x00487644`: one description, the pattern `*.SAV`, and the closing empty string | the same text from `0x00487674` |
| `nFilterIndex` | 1 | 1 |
| `lpstrFile`, `nMaxFile` | the slot's name buffer, 260 | the same |
| starting name | what the buffer holds: a single space after startup (FND-PLATFORM-010), or the last name used in slot 3 | the game's current name, copied in from the length-prefixed string at `0x00498200` |
| `lpstrFileTitle` | a 256-byte stack buffer, not read afterwards | the same |
| `lpstrInitialDir` | `.\` (`0x00487B3C`) | the same |
| `lpstrTitle` | string at `0x00487660` | string at `0x00487690` |
| `Flags` | `0x1810`: `OFN_SHOWHELP`, `OFN_PATHMUSTEXIST`, `OFN_FILEMUSTEXIST` | `0x816`: `OFN_OVERWRITEPROMPT`, `OFN_HIDEREADONLY`, `OFN_SHOWHELP`, `OFN_PATHMUSTEXIST` |
| `lpstrDefExt` | `*.SAV` (`0x0048766C`) | `*.SAV` (`0x004876AC`) |
| `lpfnHook`, `lpTemplateName` | none | none |

- When the open dialog returns nonzero, `fn_0042AFDD` sets the slot's name
  flag and returns 1. When the save dialog returns nonzero, `fn_0042B27A`
  creates the file at once with `CreateFileA` for reading and writing, read
  and write sharing and `CREATE_ALWAYS` (`0x0042B574`), closes it, sets the
  name flag and returns 1; it returns 0 when that call fails. Both functions
  finish with `SetCurrentDirectoryA` on the install directory (`0x0042B264`,
  `0x0042B5F9`).
- Open: `fn_004637B8`, called from `WinMain` for the Open command, runs the
  load `fn_0046381A` only when the dialog returned 1. A load result of 0 brings
  up `MessageBoxA` on the main window, caption at `0x004878C4`, text at
  `0x004878D0`, type 0 (an OK button); the text says the saved game is an old
  version and cannot be loaded. The load itself beeps (`fn_00449C8E`) before it
  returns 0 for a wrong closing marker or an unknown opening marker
  (FND-SAVE-001).
- The load accepts either long marker at the end whatever the opening marker
  was (`0x00463C2A`, `0x00463C37`), and all 44 blocks, and for `N40W` the
  participant block, are already in the live globals when it compares; a wrong
  closing marker leaves them there and only the three preference bytes stay
  in their staging copies.
- Save: `fn_00463CC5` (called from planning `fn_0046FD80`, `fn_004396C0` and
  `fn_00471F06`) calls the save dialog on slot 3 with the name at
  `0x00498200`. After a 1 it calls `fn_00465BC8(4, 1)`, then, only when the
  network flag `0x00487B58` is set, `fn_00458155`. When that result is
  nonzero, or at once outside network play, it copies the chosen name back to
  `0x00498200` with `fn_0042B7F3`, opens slot 3 and writes the file
  (FND-SAVE-001), closes it, sets `0x00487834` and calls `fn_00464AE6(1)`.
  It returns 1 whenever the dialog returned 1, whether or not the file was
  written, and 0 when the dialog was cancelled.
- `fn_00458155` shows dialog 144 through `fn_00465DD5`, sends a message of
  kind 14 through `fn_004688CA` to each player whose controller is 3 (a human
  over the network) and marks the others as answered in `0x00494BF4..`. It
  then takes events through `fn_00462579` until every player is marked or
  `fn_00465B27` reports the dialog closed, closes the dialog (`fn_00465E1E`)
  and returns 1 when every player answered. When the dialog closes first on
  the first pass, the byte it returns (`[EBP-0xC]`) was never written.

`M10W`. The only instruction in the executable holding `0x5730314D` is the
comparison in the load at `0x00463C4E`; the save writes `0x57303453` or
`0x5730344E` only (`0x00463D53`, `0x00463D5F`). The 12-byte block an `M10W`
file carries is read into `0x00498330..0x0049833B`, and the push of that
address at `0x00463C64` is the only reference to the range. What `WinMain`
does with the result 3 is in FND-PLATFORM-009: it calls routines that do
nothing and ends the program.

## Interpretation

Saves are ordinary files the player names in the standard dialogs, starting in
the install directory and offering `*.SAV`. Windows asks before replacing an
existing file. Choosing a name in the save dialog creates or empties that file
before the game writes it, so a save refused by a network player, or a crash
between the dialog and the write, leaves an empty file under that name; an
existing save chosen for replacement is lost in that case. A load that fails
on its closing marker has already replaced the match in memory. No build of
this executable produces an `M10W` file, and opening one ends the program;
the form looks like an unfinished feature.

## Alternatives

How the common dialog treats `lpstrDefExt` given as `*.SAV` rather than
`SAV` (which extension, if any, it appends to a name typed without one) is not
settled by reading the executable; a run would show it. What the player may do
in dialog 144 is not recorded here.

## How to reproduce

Follow the callers of the thunks at `0x00478630` and `0x00478636`, then the
callers of `fn_0042AFDD` and `fn_0042B27A`. Search the executable for the
scalar `0x5730314D` and list the references to `0x00498330..0x0049833B`.
