---
id: FND-EXE-002
title: The executable has six sections, and .data has a large zero-initialized tail
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00400178..0x00400268
tool: Ghidra 12.1.3
environment: null
---

## Observation

The section table at `0x00400178` has six entries:

| Section | Virtual address | Virtual size | File offset | File size | Flags |
|---|---|---|---|---|---|
| `.text` | `0x00401000` | `0x7FAC2` | `0x400` | `0x7FC00` | code, execute, read (`0x60000020`) |
| `.rdata` | `0x00481000` | `0xC4D` | `0x80000` | `0xE00` | initialized data, read (`0x40000040`) |
| `.data` | `0x00482000` | `0x2B110` | `0x80E00` | `0x7A00` | initialized data, read, write (`0xC0000040`) |
| `.idata` | `0x004AE000` | `0x19FA` | `0x88800` | `0x1A00` | initialized data, read, write (`0xC0000040`) |
| `.rsrc` | `0x004B0000` | `0x11BEC` | `0x8A200` | `0x11C00` | initialized data, read (`0x40000040`) |
| `.reloc` | `0x004C2000` | `0x6498` | `0x9BE00` | `0x6600` | initialized data, discardable, read (`0x42000040`) |

`.data` has `0x7A00` bytes in the file and `0x2B110` bytes in memory, so
everything from `0x00489A00` to `0x004AD110` starts as zeros.

## Interpretation

The game's global state lives in `.data`, most of it in the zero-initialized
part. Several globals that the save file copies (FND-SAVE-001) lie in that
range, such as the 15,552-byte block at `0x00498DA8`.

## Alternatives

None known.

## How to reproduce

Read the section table that follows the optional header of
`Chaos Overlords.exe` (file offset `0x178`).
