---
id: FND-COMBAT-001
title: The whole-turn resolver makes attack rolls, then police rolls, in player slot order and then roster slot order
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

- In the whole-turn resolver `0x00472775`, the block for action 1 (Attack) sits
  inside a scan of player slots 0 to 5 and, within each, roster slots 0 to 80.
  The test of the gang's action and the whole attack and retaliation
  calculation, with its random draws, happen inside one pass of that scan.
- After that scan ends, a second scan of player slots 0 to 5 and roster slots
  0 to 80 visits the active gangs in sectors under Crackdown. Each police
  detection roll and police damage roll is made inside that second scan.

## Interpretation

The order of attack rolls and results is fixed by player slot and then roster
slot, whatever order the orders were given in. Two gangs that attack each other
each make their own attack and retaliation when the scan reaches them. The
police visit gangs in the same player and roster order, after every gang
attack, and not in sector order.

## Alternatives

None known. The instruction addresses of the two scans have not been recorded;
they are placed here by their position in the resolver.

## How to reproduce

Open `0x00472775`, find the switch or comparison on the gang's action byte
(record offset `0x07`) against 1 inside the nested player and roster loops, and
the later nested loop that reads each gang's sector record at offset `0x0F`
(police presence).
