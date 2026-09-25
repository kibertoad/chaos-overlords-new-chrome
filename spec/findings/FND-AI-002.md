---
id: FND-AI-002
title: The dispatcher maps scenario and hire role to a family, and keeps the family for unmapped pairs
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00432DA0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00402D70
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00482128..0x00482140
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the state-query function `0x00402D70`, selector 0 returns the global at
`0x004ABBE8` (the scenario) and selector `0x7C` returns the per-player 32-bit
value at `0x00482128 + player * 4`. When selector `0x48` reports that byte +1
of the gang's planning record is nonzero, `0x00432DA0` assigns a family from
the scenario (rows) and the selector-`0x7C` value (columns, 0 to 6). A dash
means the switch assigns nothing and the record keeps its current family:

| Scenario value | 0 | 1 | 2 | 3 | 4 | 5 | 6 |
|---|---:|---:|---:|---:|---:|---:|---:|
| 0 | 0 | 0 | 3 | 2 | 6 | - | 7 |
| 1 | 0 | 1 | 3 | 2 | 6 | 5 | 7 |
| 2 | 0 | 0 | 5 | 2 | 6 | - | 7 |
| 3 | 0 | 0 | 5 | 2 | 6 | 3 | 7 |
| 4 | 0 | 1 | 3 | 2 | 6 | 5 | 7 |
| 5 | 0 | 1 | 3 | 2 | 6 | 5 | 7 |
| 6 | 0 | 13 | 14 | - | 6 | 5 | 7 |
| 7 | 10 | 0 | 3 | 11 | 12 | - | 7 |
| 8 | 0 | 13 | 14 | 3 | - | - | - |
| 9 | 0 | 1 | 3 | 2 | 6 | 3 | - |

A column value outside 0 to 6 also keeps the current family. Every branch that
assigns a family in column 4 also copies the signed value of selector `0x5A`
(the gang's current sector) into a 16-bit field of the gang's AI records.
After the family dispatch the function compares the scenario with 6, 7 and 8.

## Interpretation

The per-player value at `0x00482128` is the hire role the planner chose most
recently (FND-AI-009), and each gang takes the family its player's current
role maps to in the current scenario. The scenario values follow the order
Greed, Power, Acceptance, Dominance, Kill 'Em All, Big 40, Eliminate, Siege,
Big Man, Armageddon for 0 to 9: rows 6 and 8 hold the families that seek the
Eliminate headquarters and the Big Man centre (FND-AI-039), and row 7 holds
families 10, 11 and 12, which the Siege-specific code paths use (FND-AI-033).

## Alternatives

The older notes call the 16-bit field that column 4 writes the +12 word of the
planning record, which is also the weapon cooldown (FND-AI-019). FND-AI-015
describes the same write as the second 16-bit value of the 14-byte auxiliary
record. The instruction's destination address has not been recorded, so which
field it writes is open.

The numbering of scenarios 6 and 7 is contested: FND-UI-033 and FND-TURN-003
read 6 as Siege and 7 as Eliminate.

## How to reproduce

In `0x00432DA0`, find the call to `0x00402D70` with selector `0x48`, then the
nested switch on selector 0 (scenario) and selector `0x7C` (hire role) that
stores into byte 0 of the planning record. The selector cases sit in the switch
of `0x00402D70`.
