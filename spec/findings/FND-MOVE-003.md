---
id: FND-MOVE-003
title: The Terminate and Move passes skip inactive gangs, the destination is target, and selector mode 0 draws one of the eight neighbours, which the Move repair stores as the new destination
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004750A2..0x0047527F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00476A94..0x00476F3A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040867F..0x00408A17
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00441D89
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00442011
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges of the functions named here are in FND-EXE-004.

The Terminate pass of the whole-turn resolver `fn_00472775` is the player and
roster double loop at `0x004750A2..0x00475180`. For each gang record it copies
the 32 bytes into a local copy, compares the copied sector byte with 100 at
`0x00475119` and skips the record when equal. For an active record it compares
the action byte with 14 at `0x00475129`; on a match it stores 100 in the
copy's sector byte (`0x00475132`) and sets the record's changed flag in the
81-entry per-player array at `0x00498990` (`0x0047513F`). Every active record
is copied back at `0x00475153..0x0047517D`. The pass calls no function and
touches no per-player total.

The Move pass follows at `0x00475189`. For each player slot 0 to 5 it first
calls `fn_00476A94` with the player (`0x004751B2`), then scans the roster:
sector byte compared with 100 at `0x0047520F`, action compared with 10 at
`0x0047521F`, and on a match the byte at offset `0x08` (`target`) stored into
the copy's sector byte (`0x0047522E`) and the changed flag set
(`0x0047523A`). The pass calls nothing else and records no report.

`fn_00476A94(player)`:

- The recount (`0x00476AE0..0x00476C2F`) clears two 64-entry arrays and the
  mover list, then for each roster slot whose sector byte is not 100
  (`0x00476B8C`) either adds one to the first array at the byte at offset
  `0x08` and appends the slot to the mover list, when the action is 10
  (`0x00476BB2`), or adds one to the second array at the gang's sector.
- The scan at `0x00476C3A..0x00476C86` compares the sum of the two arrays with
  6 for each sector and keeps the last sector above 6.
- The first repair loop (`0x00476CA7..0x00476DB3`) takes the first mover whose
  offset `0x08` byte is that sector and whose own sector's sum of both arrays
  is below 6 (`0x00476D3F`), and stores the mover's sector byte into its
  offset `0x08` byte (`0x00476D96`).
- If none qualified, the fallback loop (`0x00476DDF..0x00476F20`) takes the
  first mover whose offset `0x08` byte is that sector. When that byte already
  equals the mover's own sector (`0x00476E4C`), it calls
  `fn_00408642(player, 0, slot)` (the literal 0 is the second argument, pushed
  at `0x00476EA4`) and stores the returned value into the offset `0x08` byte
  (`0x00476EB8`). Otherwise it stores the mover's sector there
  (`0x00476F03`).
- It repeats from the recount while a sector above 6 was found.

In `fn_00408642`, the test of the second argument against 0 at `0x0040867F`
selects the mode 0 block. That block calls the bounded random wrapper
`fn_0045D227` with 8 (`0x0040868B`), subtracts 1 and dispatches through the
jump at `0x004089D5` (table `0x004089DC`) to one of eight cases.
Draws 1 to 8 form the candidate
`sector - 9`, `- 8`, `- 7`, `- 1`, `+ 1`, `+ 7`, `+ 8` and `+ 9` from the
gang's sector byte, and mark the candidate invalid when it would leave the
8-by-8 grid: column 0 for the three western offsets, column 7 for the three
eastern ones, sector below 8 for the three northern ones and above 55 for the
three southern ones. A candidate whose owner byte in the sector table is
below -1 is also marked invalid (`0x00408A02..0x00408A13`). The loop head at
`0x0040865A` returns the candidate once it is valid and within 0 to 63, and
otherwise draws again. Mode 0 builds no score map, calls no selector case and
makes no capacity test.

The Move panel handler `fn_004413EF` writes the chosen destination sector into
offset `0x08` of the gang record at `0x00441D89` (pointer confirmation) and
`0x00442011` (keyboard confirmation). The candidates are the eight sectors at
the same eight offsets from the gang's sector, chosen from a 3-by-3 grid whose
centre cell is excluded (`0x004420E9..0x004421B1`).

## Interpretation

- A gang that died in this turn's combat carries out neither Terminate nor
  Move. A Terminate or Move creates no report and changes no statistic.
- A Move's destination is the `target` byte. The fallback of the repair gives
  a mover whose destination is its own crowded sector a random neighbouring
  destination, drawn with one `roll(8)` per attempt and retried while the
  neighbour is off the map. The neighbour is not checked against the six-gang
  limit here; the next recount decides whether it has to be repaired again.
- FND-MOVE-001's description of mode 0 as one draw over all 64 sectors
  followed by one-step routing does not match these instructions; FND-AI-005's
  description of a neighbour draw does.

## Alternatives

- The owner test in the mode 0 loop can only reject a candidate whose owner
  byte is below -1, which no sector holds, so it has no effect on a valid
  candidate.
- Whether the repair loop always ends was not settled: a random neighbour can
  itself be crowded, and the loop ends only when a recount finds no sector
  above 6.

## How to reproduce

In `fn_00472775`, find the two player-then-roster loops after the Chaos payout
(`0x004750A2` and `0x00475189`) and their comparisons with 100, 14 and 10. In
`fn_00476A94`, follow the two arrays of 64, the comparison with 6 and the call
at `0x00476EAA`. In `fn_00408642`, the branch at `0x0040867F` leads to the call
with 8 at `0x0040868B` and the eight offset cases.
