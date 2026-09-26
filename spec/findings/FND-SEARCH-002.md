---
id: FND-SEARCH-002
title: The Search panel's ALL, NONE and Done controls and its 22 row targets
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00448E32
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004718EE
tool: Ghidra 12.1.3
environment: null
---

## Observation

- `fn_00448E32` is reached from the lower (Search) part of the Ranking and
  Search control in the main-console dispatcher `fn_004718EE`. It loads
  `PX05024`, sets up its rows from the active player's 22 filter bytes and
  opens the standard panel.
- It limits pointer input to `(104,124)-(448,333)` and converts it to
  panel-local coordinates. Its half-open rectangles are:

| Control | Rectangle | Effect |
|---|---|---|
| ALL | `(33,16)-(82,39)` | Sets all 22 bytes to 1 and draws the whole list again |
| NONE | `(33,48)-(82,71)` | Sets all 22 bytes to 0 and draws the whole list again |
| Done | `(33,169)-(82,191)` | Goes through the shared press and release helper and closes |
| Row `n` | from `(102 + 116 * (n / 11), 22 + 15 * (n % 11))`, 114 wide and 15 high | Flips byte `n`; a double-click opens Site Information instead |

- ALL and NONE use the shared pressed-control sprites 4 and 5.
- Enter and Execute activate Done. The handler has no branch for the right
  mouse button or for other keys.

## Interpretation

The rows form two columns of 11, 116 pixels apart, with a row pitch of 15.
ALL and NONE are 23 pixels high, one more than the 22 of Done.

## Alternatives

None known.

## How to reproduce

Open `fn_00448E32` and read the rectangle constants, the row arithmetic with
116, 15 and 11, the calls to the shared pressed-control helper with sprites 4
and 5, and the virtual-key comparisons with `0x0D` and `0x2B`.
