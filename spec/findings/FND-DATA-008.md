---
id: FND-DATA-008
title: No code path in the executable can open DATA/DATA.Z
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AE620
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042B60F..0x0042B7F2
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AE974..0x004AE9B3
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges of the functions named here are in FND-EXE-004.

The executable's only import that opens a file by name is `CreateFileA`
(slot `0x004AE620`); there is no `OpenFile`, `_lopen`, `LZOpenFile` or
`mmioOpen`. It is called from nine places in these functions:

| Function | What it opens |
|---|---|
| `fn_004282AA` | `data\CLT` plus five digits, always `CLT00002` (FND-PLATFORM-011) |
| `fn_004273D5` | `data\PX08\PX` or `data\PX16\PX` plus five digits (FND-PLATFORM-002) |
| `fn_0042B60F`, `fn_0042AC7A` | the name held in a file slot (FND-PLATFORM-010): the three definition tables, `SNDnnnnn` sounds, the two image probes, a file named on the command line, and the `CDTrack` name of the unused `fn_004667DC` |
| `fn_0042B27A` | the name chosen in the save dialog (FND-SAVE-002) |
| `fn_00424E55`, `fn_00424FB4` | `COM1` to `COM4` (FND-PLATFORM-013) |
| `fn_0042885B`, `fn_00426F77` | no instruction calls either and no four-byte value in the file equals their addresses |

Every fixed name among them is built from a string whose text ends in digits
the code replaces or is a complete name, and none of them contains `.Z`; the
byte strings `data.z` and `.z` followed by a NUL do not occur in the file,
in any case, nor in UTF-16. The command-line name is whatever the user
passes. The movies are opened by name through `_SmackOpen` (FND-PLATFORM-013)
from the `data\mvintro` and `data\mvlogos` strings, and the help through `WinHelpA`.

In the installation, `DATA\DATA.Z` is named in `goggame-galaxyFileList.ini`,
the GOG client's file list, and in the uninstall log `unins000.dat`. No file
of the original 1996 installer (a setup program or `.ins` script) is shipped.

## Interpretation

The game never reads `DATA.Z`. It is the compressed payload of the original
InstallShield 3 setup (FND-DATA-005), which the GOG release copied into the
data folder along with the files it had already expanded.

## Alternatives

Only opening the file by a name typed on the command line, or through Open in
the File menu, would pass it to the game; neither treats it as anything other
than a save file, which it is not.

## How to reproduce

List the references to the `CreateFileA` import slot and follow each call's
name argument back to its string. Search the executable for `data.z`,
ignoring case, in single-byte and UTF-16 text.
