---
id: FND-UI-050
title: The city compositor keys a police badge over every sector with police presence, after the site markers and before the gang-status marker
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004128B0..0x00412AB5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004129CE..0x00412AA2
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function extents are those of FND-EXE-004. The city compositor `fn_004123CC`
(FND-UI-025) ends with a loop over the 64 sectors, `0x004128B0` to
`0x00412AB5`. For sector `s`, at column `c = s % 8` and row `r = s / 8`, it:

- calls `fn_00412AC4` for each of the sector's site slots that the site
  markers draw (FND-SEARCH-003);
- at `0x004129CE` reads the signed byte `0x004A08F7 + 0x24 * s`, which is
  `crackdown_turns` at offset `0x0F` of the sector record (FMT-STATE-002), and
  when it is greater than 0 copies the rectangle with top 560, left 317,
  bottom 588 and right 337 in the packing of `fn_00425EDF` (FND-GFX-004) from
  surface 6, the interface sheet `PX00129`, to surface 2, the city map, with
  `fn_00427864` and its keyed flag set (`0x00412A9D`). The destination is
  `fn_00425F4D` with the corner `(53c + 9, 51r + 14)` and the size 20 by 28
  (`0x004129E4` to `0x00412A27`);
- at `0x00412AAD` calls `fn_00412BF7` with the player argument and the
  sector, which draws the gang-status marker (FND-UI-031).

Nothing in the loop tests the player argument before the copy.

## Interpretation

Every sector under police presence, a Crackdown of any length, carries a
badge on the city map whoever looks at it. The packing of `fn_00425EDF`
gives the source as the 20-by-28 area from x 317 to 337 and y 560 to 588 of
`PX00129`, the same size as the destination. The badge lies over the site
markers and under the gang-status marker. The detailed sector screen shows it
in its nine-sector display, which is copied from the prepared map
(FND-UI-018).

## Alternatives

None known.

## How to reproduce

In `fn_004123CC`, find the loop that compares the sector index with `0x40`
at `0x004128BF`. After the inner loop over three site slots, find the read of
`0x004A08F7` at `0x004129D4`, the `jle` that skips the copy, the pushes of
`0x14` and `0x1C` before `0x00425F4D`, the pushes of `0x230`, `0x13D`,
`0x24C` and `0x151` before `0x00425EDF`, and the call of `0x00427864` with 6,
2 and 1.
