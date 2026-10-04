---
id: FND-SEARCH-006
title: The city redraw passes each site marker's definition, sector, ordinal and controlled flag to fn_00412AC4 from two calls, and takes the viewing player as its first argument
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00412900..0x004129C6
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the city redraw `fn_004123CC`, the site loop of FND-SEARCH-003 compares
each sector's owner byte at `0x004A08E8 + sector * 0x24` with the function's
first argument `[EBP+0x8]` (`0x00412937`), after comparing the site's
progress with its definition's Resistance at `0x004AB67E` (`0x00412921`). For
a controlled site it calls the marker renderer at `0x0041295F`; otherwise it
reads the filter byte of `[EBP+0x8]` for the site's definition in the table
at `0x004A24E8` (`0x00412990`) and, when it is set, calls the renderer at
`0x004129BE`.

Both calls push, from the first argument: the site's definition byte at
`0x004A08EF + sector * 0x24 + slot * 2`, the sector `[EBP-0xC]`, the ordinal
`[EBP-0x4]`, and 1 at `0x0041295F` or 0 at `0x004129BE`. The ordinal goes up
by one after each call (`0x00412967`, `0x004129C6`). The renderer
`fn_00412AC4` takes these four arguments, the caller removing 16 bytes.

## Interpretation

The redraw's first argument is the player whose city is drawn, the
`active_player` of RULE-SEARCH-002. A debugger that stops at
`fn_00412AC4` during a redraw reads one marker per call: definition, sector,
ordinal and whether the site is controlled.

## Alternatives

None known.

## How to reproduce

Open `fn_004123CC` at `0x00412900` and follow the comparison with
`[EBP+0x8]`, the read of `0x004A24E8`, and the two calls of `fn_00412AC4`
with the four values each pushes.
