---
id: FND-TOLERANCE-001
title: Bribe, Snitch and a one-point drift change the base Tolerance at sector offset 0x02, which is clamped to 1..40 after the instant phase, while the Chaos test reads offset 0x05
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472964..0x00472AD8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472B6B..0x00472BF5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00473046..0x0047307E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047310B..0x00473187
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047358D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046B0FC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00498B78..0x00498BB7
tool: Ghidra 12.1.3
environment: null
---

## Observation

All addresses below are in the whole-turn resolver `fn_00472775` unless
another function is named. The sector records are at `0x004A08E8 + sector *
0x24` (FMT-STATE-002).

- FND-STATE-001 lists every writer of the byte at offset `0x02`
  (`0x004A08EA`) and shows that the byte at offset `0x05` (`0x004A08ED`) is
  written only by the refresh before planning, as the byte at `0x02` plus the
  completed sites' Tolerance. This finding assigns the resolver's writes to
  their blocks and records how they are made.
- Drift. The sector loop from `0x00472964` to `0x00472AD8`, near the start of
  the resolver and before the instant-phase scan of players and roster slots,
  visits sectors 0 to 63. For each it computes 17 minus the signed byte at
  offset `0x01` (`0x00472A42..0x00472A5A`). If the byte at `0x02` is less than
  that value it is incremented with a byte `INC` at `0x00472A86`; it is then
  read again, and if it is greater than the value it is decremented with a
  byte `DEC` at `0x00472AC0`. Each change also stores 1 in the sector's entry
  of the 64-byte array at `0x00498B78` (`0x00472A93`, `0x00472ACD`). The same
  loop stores 0 in that entry first, at `0x0047298C`, for every sector. The
  loop reads nothing else to decide the move.
- The instant-phase switch dispatches on `action - 2` through the byte table at
  `0x004730C9` and the jump table at `0x004730AD`.
- Bribe, case 2, is `0x00472B6B..0x00472BF5`. It compares the player's cash at
  `0x004A25E8` with 3 (`0x00472B71`, `JL` to the failure call). On success it
  subtracts 3 from cash (`0x00472B85`), reads the byte at offset `0x02` with
  `MOVSX`, adds 3 in a 32-bit register (`0x00472B9F`), stores the low byte back
  (`0x00472BAC`), stores 1 in the sector's entry of `0x00498B78`
  (`0x00472BBA`) and adds 3 to cash spent at `0x0049CA78` (`0x00472BC7`). The
  failure path calls the report helper `fn_00477748` with report type 6
  (`0x00472BE9`) and sets no mark.
- Snitch, case 13, is `0x00473046..0x0047307E`. It reads the byte at offset
  `0x02` with `MOVSX`, subtracts 3 in a 32-bit register (`0x00473058`), stores
  the low byte back (`0x00473065`) and stores 1 in the sector's entry of
  `0x00498B78` (`0x00473073`).
- Clamp. After the scan ends, the loop at `0x0047310B..0x00473187` visits
  sectors 0 to 63. When the signed byte at offset `0x02` is less than 1 it
  stores 1 (`0x00473150`); it then reads the byte again, and when it is greater
  than 40 (`0x28`) it stores 40 (`0x0047317B`).
- The Chaos sector pass compares the signed byte at offset `0x05` with the
  sector's Chaos total at `0x0047358D`, a crackdown following when the total is
  greater.
- The array at `0x00498B78` has one reader: the network session host
  `fn_0046A7CB`, whose loop at `0x0046B0E3..` tests each sector's entry
  (`0x0046B0FC`) and for each marked sector writes a 36-byte block into its
  outgoing message. The other writers of the array, all storing 1, are at
  `0x00472EC8` (Influence), `0x004737CC`, `0x00475856` and `0x00475E81` in the
  resolver, and `0x0047700C` in `fn_00476F3B`.

## Interpretation

The byte at offset `0x02` is the sector's base Tolerance, and the byte at
offset `0x01` its base Income: the drift pulls the base Tolerance one point
per turn toward 17 minus the base Income, the value city generation starts it
at. The drift ignores sites; the site Tolerance is added only when the record is
rebuilt before planning, into offset `0x05` (FND-STATE-001).

Each turn's resolution therefore works on the base Tolerance in this order:
one point of drift toward normal, then every Bribe (+3) and Snitch (-3) in
player and roster order, then a clamp to 1..40. The Chaos test of the same
resolution reads offset `0x05`, which holds the base Tolerance and site
Tolerance as they stood at the rebuild before planning, so a Bribe or Snitch
changes the crackdown test from the next turn on, not in its own turn. The cap
of 40 exists, as the manual's maximum, but in the clamp after the phase and
for every sector, instead of in the Bribe case.

The Bribe and Snitch arithmetic is done in 32 bits and truncated to a signed
byte on the store. Before the Bribes of a turn the base Tolerance is at most 40,
so it wraps only when a thirtieth Bribe in one sector in one turn takes it past
127; it then becomes negative and the clamp raises it to 1.

The array at `0x00498B78` marks sectors whose record changed during the
resolution, so that the host of a network game sends those records to the
other machines. Bribe sets the mark only when it succeeds.

## Alternatives

Whether the Chaos pass of this build could read offset `0x02` through some
path other than the instruction at `0x0047358D` was checked only through the
references to `0x004A08EA` and `0x004A08ED`; a computed pointer into the
sector list would not appear there.

## How to reproduce

List the references to `0x004A08EA`, `0x004A08ED` and `0x00498B78`. In
`fn_00472775`, read the loop that starts at `0x00472964` (the constant
`0x11`), the cases at the jump targets `0x00472B6B` and `0x00473046`, the loop
at `0x0047310B` (the constants 1 and `0x28`) and the comparison at
`0x0047358D`.
