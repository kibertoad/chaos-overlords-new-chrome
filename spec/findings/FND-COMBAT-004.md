---
id: FND-COMBAT-004
title: The resolver keeps a 10-byte combat record per gang, and a retaliation is written into the attacker's record
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
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A11E8..0x004A24E4
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The whole-turn resolver `0x00472775` keeps one 10-byte record per gang at
  `0x004A11E8 + 10 * (player * 81 + roster_slot)`, 486 records in all.
- Near the end of the combat work, after the police scan, the resolver fills
  each record as follows, as read from the decompiled code:
  - byte 0: a byte the reading calls the gang definition;
  - bytes 1, 2 and 3: the gang's Force at the start of the phase, its final
    Force, and its displayed Force;
  - byte 4: the damage of the gang's opening attack, or -1 when the target
    evaded;
  - byte 5: the retaliation damage the gang took;
  - bytes 6, 7 and 8: the gang's weapon, armor and miscellaneous item;
  - byte 9: the police damage the gang took, or -1 for none.
- The retaliation roll inside the attack block writes into the record slot of
  the attacking gang. No record is written for the retaliation on its own.
- A later part of the same combat work fills a per-sector table: a counter per
  player and per sector hands the next slot of that player's row in the sector
  to each gang that fought, in the order the player and roster scan reaches
  the gangs.

## Interpretation

An attack and the retaliation it provokes share the attacker's record. A gang's
slot in a sector's row is its rank, in roster order, among its owner's gangs
that fought in that sector.

## Alternatives

- Byte 0: this reading calls it the gang definition. The reading of the AI
  placement helper (FND-AI-010) calls byte 0 of record 0 the gang's owner.
  Neither reading gives the address of the instruction that writes the byte,
  and neither says which byte of the gang record (FMT-STATE-001) is copied into
  it. The gang record's first two bytes are placed only by
  SRC-RECHAOS-3561D41 (owner at `0x00`, definition at `0x01`), so the two
  readings may name the same copy differently. The byte stays unsettled until
  the store instruction and its source offset are recorded.
- Whether the fill writes records of gangs that did not fight, or leaves their
  earlier values, was not recorded.
- Which gangs count as having fought for the per-sector table (attackers,
  targets, gangs the police hit) was not recorded.

## How to reproduce

In `0x00472775`, find the stores whose address is computed as
`0x004A11E8 + 10 * (player * 81 + slot)` after the police scan, and the store
into the attacker's slot inside the retaliation code of the attack block.
