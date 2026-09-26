---
id: FND-SNITCH-001
title: Snitch subtracts 3 from Tolerance with no cash test, and after the instant phase every Tolerance below 1 becomes 1
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472775
tool: Ghidra 12.1.3
environment: null
---

## Observation

- Inside the instant-phase switch of the whole-turn resolver `0x00472775`,
  the case for action 13 (Snitch) subtracts 3 from the Tolerance byte of the
  sector record and marks the sector as changed. It reads no cash and has no
  failure branch.
- After the whole instant-phase scan of players and roster slots has
  finished, a loop over the sectors sets every Tolerance below 1 to exactly 1.

## Interpretation

Snitch always works, even for a player in debt, and lowers Tolerance by 3
first. No per-command floor applies; the one floor after the instant phase
keeps every sector's Tolerance at 1 or more for the rest of the turn, so a
sector never reaches the Chaos phase with a Tolerance below 1.

## Alternatives

The manual's minimum of 0 for the base Tolerance is not in this case. What
the "changed" mark is used for has not been read.

## How to reproduce

In `0x00472775`, find the instant-phase switch; case 13 subtracts the
constant 3 from the sector's Tolerance byte. The loop that follows the scan
compares each sector's Tolerance with 1. The instruction addresses of the case
and the loop have not been written down.
