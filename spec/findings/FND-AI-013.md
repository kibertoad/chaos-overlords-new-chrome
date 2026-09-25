---
id: FND-AI-013
title: AI queries for the first sector holding a visible hostile human gang and for family-6 coverage
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00402D70
tool: Ghidra 12.1.3
environment: null
---

## Observation

Selector `0x90` of `0x00402D70`, given an observing player and a sector, scans
every other player's 81 gang records for a gang whose sector byte equals the
sector and whose visibility byte for the observer is set. It returns 10 when
that gang's owner has controller type 0 or 3 and the observer's attitude
toward that owner is negative, 1 for any other such visible gang, and 0 when
there is none.

Selector `0x9A`, given an ordinal, returns the sector with that ordinal among
the sectors whose cached selector-`0x90` value is 10, or 100 when there is
none. The hire planner calls it with ordinal 1, so it gets the first sector, in
ascending order, that holds a visible hostile human gang.

Selector `0x5F` scans the player's active family-6 gangs and returns one whose
current sector, or cached destination, equals a given sector; otherwise it
returns -1. It uses the cached destination (the second 16-bit value of the
auxiliary record, FND-AI-015) only when the first 16-bit value of that record
is -1; otherwise it tests the gang's current sector.

## Interpretation

Weight 10 marks a sector where the computer can see a gang of a human player it
is hostile to. The hire planner uses these queries to send a new family-6 gang
toward the first such sector that no family-6 gang already covers.

## Alternatives

Whether selector `0x90` returns on the first visible gang it finds, or looks on
for a gang that earns 10, is not stated; FND-AI-039 reads it as deciding on the
first visible gang. Whether the ordinal of selector `0x9A` counts from 1 is
inferred from the hire planner's use of 1 for the first sector.

## How to reproduce

Find the cases for selectors `0x90`, `0x9A` and `0x5F` in the switch of
`0x00402D70`. Selector `0x90` compares the gang sector byte (stride `0x20`
from `0x00498DAA`) and the per-observer visibility byte, reads the controller
array at `0x004AB638` and the attitude matrix at `0x004AB590`.
