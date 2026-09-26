---
id: FND-CONTROL-002
title: Inside the whole-turn resolver only Crackdown neutralization and a Control win write a sector's owner
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

Every write in `fn_00472775` to the owner byte at
`0x004A08E8 + sector * 0x24` was listed. There are two:

- In the Crackdown history block of the Chaos pass, a third qualifying
  Crackdown sets the owner to -1 and clears the sector's influence totals.
- In the Control block, a winner other than -1 replaces the owner and clears
  the same totals (FND-CONTROL-001).

The Terminate and Move passes contain no write to the owner byte.

## Interpretation

Moving or terminating the last gang a player has in a sector does not give up
the sector. The owner keeps it until a Crackdown neutralizes it or another
player wins it with Control. A later Control attempt against the sector still
has to beat its Income and Support, with no defending gangs.

## Alternatives

The audit covered the whole-turn resolver only. Owner writes elsewhere, such
as in city generation, loading a save or the Eliminate scenario's clean-up,
were not part of it.

## How to reproduce

In `fn_00472775`, list the cross-references to the sector table at
`0x004A08E8` that store to offset `0x00` of a record. Two stores remain: one in
the Crackdown history block after the Chaos rolls and one in the Control
block.
