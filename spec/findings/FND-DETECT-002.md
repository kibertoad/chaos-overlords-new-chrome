---
id: FND-DETECT-002
title: The visibility rebuild runs for all six observer slots, writes 0 before 1 for every active opposing gang, and leaves inactive records alone
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046FA11..0x0046FD7F
tool: Ghidra 12.1.3
environment: null
---

## Observation

In `fn_0046FA11` (range in FND-EXE-004):

- The outer loop counts the observer from 0 to 5 (`0x0046FA1D`) with no test
  of whether the slot holds an active player. The only per-observer read
  before the sector strengths is the byte `0x004AB588 + observer`
  (`0x0046FA5D`), which picks the starting strength 1000 or -32000.
- The first scan of the observer's 81 records (`0x0046FAA8..0x0046FB60`)
  skips a record whose sector byte is 100 and keeps, per sector, the first
  record with the strictly highest Detect (offset `0x15`).
- The second scan (`0x0046FB6C..0x0046FC58`) stores 1 in the observer's own
  byte of each of its active records, offset `0x0C + observer`
  (`0x0046FBCD`), and adds the helper bonus for each active record other than
  the sector's chosen one.
- The third scan (`0x0046FC64..0x0046FD51`) visits the other five player
  slots (`0x0046FC89` skips the observer) and their 81 records. For a record
  whose sector byte is not 100 it first stores 0 in byte `0x0C + observer`
  (`0x0046FCF9`), then stores 1 there (`0x0046FD51`) when the record's
  Stealth (offset `0x14`) is less than or equal to the observer's strength in
  that sector (`0x0046FD1B`). A record whose sector byte is 100 is not written.

## Interpretation

Visibility is rebuilt for all six slots, including slots of players who are
out of the match or never joined. A gang that is not seen has its byte set to
0, so no stale visibility survives for active gangs. The bytes of an inactive
record keep whatever they held when the gang died, left or was hired over.

## Alternatives

None known.

## How to reproduce

In `fn_0046FA11`, read the loop bound 6 at `0x0046FA1D`, the three record
scans and the stores into `0x00498DB4 + ...` at `0x0046FBCD`, `0x0046FCF9` and
`0x0046FD51`.
