---
id: FND-AI-028
title: The 48 direct calls of the shared sector selector, by mode and caller
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00408642
tool: Ghidra 12.1.3
environment: null
---

## Observation

The 48 direct references to `0x00408642` push these literal modes:

| Mode | Callers |
|---:|---|
| 0 | `0x00476A94`, one call |
| 2 | family 4 (`0x00401000`), four calls |
| 3 | family 9 (`0x004605E0`), two calls |
| 5 | family 0 (`0x00428EF0`), eight calls; family 1 (`0x00434080`), seven calls; family 7 (`0x00436C70`), one call |
| 6 | family 2 (`0x0041FEF0`), two calls |
| 7 | family 5 (`0x0043A1D0`), four calls |
| 8 | family 3 (`0x00435BD0`), four calls |
| 9 | family 10 (`0x0042A6E0`), two calls; dispatcher `0x00432DA0`, one call |
| 10 and 16 | family 11 (`0x00420950`), two mode 10 calls and one mode 16 call |
| 12 and 13 | family 13 (`0x0040ABC0`), one call each |
| 14 and 15 | family 14 (`0x00466910`), one call each |

Family 6 (`0x00431C60`) passes mode 2 when selector `0x60` finds no stored
target, and otherwise the target plus `0x40`. Families 7 and 12 also pass
encoded sectors. No direct call pushes the literal modes 1, 4 or 11.

## Interpretation

Each family has its own movement goal. Modes 1, 4 and 11 can only be reached
through a computed argument, if at all.

## Alternatives

The dispatcher's mode 9 call is described in the older notes as a
scenario-specific Move for roster slot 0; its condition is not recorded.

## How to reproduce

List the references to `0x00408642` and read the three arguments pushed before
each call.

This evidence was recorded in the older notes under the endgame awards
heading; it concerns only the AI.
