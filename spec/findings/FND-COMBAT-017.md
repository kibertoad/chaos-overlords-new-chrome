---
id: FND-COMBAT-017
title: Detailed Combat copies each clip's sector tile from the unowned city map art and frames it in black
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042E2A4..0x0042E33B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042E348..0x0042E3E7
tool: Ghidra 12.1.3
environment: null
---

## Observation

- For each sector it presents, `fn_0042E040` builds the source rectangle
  `(4 + 0x35 * column, 0x1A3 + 0x33 * row)`, 0x36 by 0x34, with `fn_00425F4D`
  (`0x0042E2A4..0x0042E2C1`), the destination with
  `fn_00425EDF(0x9B, 0x1F, 0xCF, 0x55)` at `0x0042E2F0`, and copies the one to
  the other from surface 2 to surface 7 with `fn_0042773E(2, 7, ...)` at
  `0x0042E336`.
- It then takes the colour `fn_00425E99(0, 0, 0)` at `0x0042E355`, builds the
  same destination at `0x0042E395` and draws it with `fn_00426575` at
  `0x0042E3DF`, with its fill flag 0.
- Nothing between the two reads a sector's owner.

## Interpretation

The sector tile of SCR-COMBAT-002 is the sector's 54-by-52 cell of the map art
without any owner's colour, from the same place of surface 2 the Sector Gangs
panel copies its tile from (FND-UI-014), at panel-local `(31, 11)`, under an
unfilled black frame.

## Alternatives

None found.

## How to reproduce

In `fn_0042E040`, find the constant `0x1A3` at `0x0042E2A4`, the copy from
surface 2 to surface 7 that follows it and the call of `0x00426575` with the
colour from `fn_00425E99(0, 0, 0)`.
