---
id: FND-AI-015
title: A second per-gang AI record holds a focus value and a family-6 coverage sector
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048C0BA
tool: Ghidra 12.1.3
environment: null
---

## Observation

A block of 14-byte records per player and roster slot is referenced at
`0x0048C0BA`. Its first 16-bit value is written by several family handlers:

- family 11 stores a formation sector in it (FND-AI-024);
- family 7 stores a sector while it attacks or sets up a research position, an
  item number while it researches, and -1 after an Equip or a routed Move
  (FND-AI-035);
- family 2 stores the current sector on Attack and clears it on its other
  actions (FND-AI-032).

Its second 16-bit value is set to the gang's current sector by every branch of
the dispatcher that assigns a family for hire role 4 (FND-AI-002). Family 6
stores a chosen strategic destination in it before routing, and then the actual
one-step Move destination (FND-AI-029). Selector `0x5F` reads the second value
only while the first is -1 (FND-AI-013).

## Interpretation

The first value is a family-dependent focus, and the second is the sector a
family-6 gang is heading for or covering. Both persist between turns.

## Alternatives

The stride and the base of the block are not established: `0x0048C0BA` is the
address the older notes give for "the first short", and the 16-byte planning
block ends at `0x0048C0B0`, so the block may start at `0x0048C0B0` with the
first value at +10. Whether the block is saved with the game has not been
recorded. Whether the records are indexed by `player * 81 + roster_slot` is
assumed.

## How to reproduce

Search the family handlers listed above for 16-bit stores in the region after
`0x0048C0B0`, and the dispatcher `0x00432DA0` for the store that follows the
selector-`0x5A` call in the hire-role-4 branches.
