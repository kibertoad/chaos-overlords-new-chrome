---
id: FND-COMBAT-016
title: The Detailed Combat clip player keeps its tick in a stack local, and paints the Force tracks again only on tick 16
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00430C23..0x00430C36
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004310E8..0x00431128
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004317CF..0x00431948
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00431A55..0x00431A5A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00431BD8..0x00431C54
tool: Ghidra 12.1.3
environment: null
---

## Observation

- `fn_00430C23` sets up a frame with `ebp` at `0x00430C24` and stores 0 in the
  doubleword at `ebp - 0x20` at `0x00430C2F`.
- Each pass of its loop tests timer slot 0 with `fn_004328BE(0)` at
  `0x004310EA`. When the slot is set, it clears it with `fn_004328F8(0)` at
  `0x00431100`, copies the local to a second local, and at `0x00431BD8`
  decrements the copy and dispatches on it when it is at most `0x15`, through
  the byte table at `0x00431C24` and the address table at `0x00431C00`.
  `0x00431C3A` then increments the local at `ebp - 0x20`. A pass that finds
  the slot clear skips both.
- The byte table holds, for the copy 0 to 21: 0, 8, 1 eight times, 8, 2, 3, 4,
  5, 6, 8 five times, 7. Case 0 is `0x00431116`, which plays effect slot 5.
  Case 1 is `0x00431125`, which subtracts 3 from the local at `0x00431128` and
  uses the result as the strip frame. Case 8 is `0x00431BCE`, which does
  nothing. Case 7 is `0x00431BC5`.
- Case 4, `0x004317CF..0x00431948`, builds the screen rectangles top `0xF7`,
  left `0x100`, bottom `0xFA`, right `0x13C` and top `0xF7`, left `0x149`,
  bottom `0xFA`, right `0x185`, and copies to them from surface 7 the
  rectangles top `0x10B`, left `0x98`, bottom `0x10E`, right `0xD4` and top
  `0x10B`, left `0xE1`, bottom `0x10E`, right `0x11D` with `fn_0042773E(7, 0,
  ...)`. It calls nothing else that draws.
- Case 6 starts at `0x00431A55` with calls of `fn_0042F779` and `fn_0043066C`,
  the two gangs' track painters (FND-COMBAT-015), and then makes the same two
  copies.
- The function returns at `0x00431C53`.

## Interpretation

The local at `ebp - 0x20` is the clip's tick in the numbering SCR-COMBAT-002
uses: a pass handles tick `t` while the local holds `t`, through case `t - 1`,
and leaves it at `t + 1`. The first pass finds 0, dispatches nothing and only
increments it. Ticks 3 to 10 draw the frames 0 to 7, tick 12 is case 2, ticks
13 to 16 cases 3 to 6, ticks 17 to 21 do nothing and tick 22 is case 7. While
the clip runs, the screen shows what the passes drew up to tick `local - 1`,
and the initial panel while the local is 0 or 1.

The tracks in surface 7 are those painted before the clip's ticks, with the
Force before the clip, until tick 16 paints them again. Tick 14 copies the
lower tracks from surface 7 over the white of tick 13, so it shows the Force
before the clip; tick 16 shows the lowered Force.

## Alternatives

None found.

## How to reproduce

In `fn_00430C23`, find the store of 0 to `[ebp - 0x20]` after the prologue,
the copy of that local at `0x00431108`, the decrement and bound test at
`0x00431BD8`, the two tables after the function's indirect jump, and the
increment at `0x00431C3A`. Follow entries 4 and 6 of the address table to the
copies of the lower tracks, and to the calls of `0x0042F779` and `0x0043066C`
that only case 6 makes.
