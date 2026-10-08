---
id: FMT-STATE-007
title: Computer player planning record, one per player and roster slot
status: established
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: []
byte_order: little
size: 16
text: false
definition: fmt_state_007.ksy
evidence: [FND-AI-019, FND-AI-021, FND-SAVE-001, FND-STATE-006, FND-AI-042, FND-AI-074, EXP-TURN-048, EXP-TURN-049, EXP-TURN-050, EXP-TURN-051, EXP-TURN-053, EXP-TURN-056, EXP-TURN-058, EXP-TURN-074, EXP-TURN-105]
conflicting: []
split_with: []
related: []
---

## Layout

The computer players keep one of these records per player and roster slot in
the list `planning_records`, at `0x0048A250 + player * 0x510 + slot * 0x10`,
486 records in all [FND-AI-019]. The block is saved whole (save block 14,
7,776 bytes) [FND-SAVE-001]. Before each planning pass, for every record whose
gang is alive, the planner copies the previous triplet into the older one,
the planned triplet into the previous one, and clears the planned triplet
[FND-AI-019]. The reset of a record stores 99 in `family`, 0 in `needs_family`, 0
in the three triplets and 0 in both cooldowns [FND-STATE-006].

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 1 | `INT8` | `family` | The planning family the record's gang follows; 99 after a reset | established | FND-AI-019, FND-STATE-006, EXP-TURN-048, EXP-TURN-053, EXP-TURN-056, EXP-TURN-058, EXP-TURN-074 |
| `0x01` | 1 | `UINT8` | `needs_family` | Set to 1 when the record needs a new family: for a slot whose gang is not alive at a planning pass, for slot 0 on a player's first pass, and by the Greed Terminate branches of seven family handlers; cleared by the reset. Read only by selector `0x48`, which makes the dispatcher reset the record and assign a family | established | FND-STATE-006, FND-AI-042, EXP-TURN-048, EXP-TURN-105 |
| `0x02` | 1 | `UINT8` | `older_action` | The action planned two turns ago, a command number of FMT-STATE-001 `action` | established | FND-AI-019, EXP-TURN-048, EXP-TURN-049, EXP-TURN-050, EXP-TURN-051 |
| `0x03` | 1 | `INT8` | `older_target` | Its first target byte | established | FND-AI-019, EXP-TURN-048, EXP-TURN-049, EXP-TURN-050, EXP-TURN-051 |
| `0x04` | 1 | `INT8` | `older_target_2` | Its second target byte | established | FND-AI-019, EXP-TURN-048, EXP-TURN-049, EXP-TURN-050, EXP-TURN-051 |
| `0x05` | 1 | `UINT8` | `previous_action` | The action planned last turn | established | FND-AI-019, EXP-TURN-048, EXP-TURN-049, EXP-TURN-050, EXP-TURN-051 |
| `0x06` | 1 | `INT8` | `previous_target` | Its first target byte | established | FND-AI-019, EXP-TURN-048, EXP-TURN-049, EXP-TURN-050, EXP-TURN-051 |
| `0x07` | 1 | `INT8` | `previous_target_2` | Its second target byte | established | FND-AI-019, EXP-TURN-048, EXP-TURN-049, EXP-TURN-050, EXP-TURN-051 |
| `0x08` | 1 | `UINT8` | `planned_action` | The action planned this turn | established | FND-AI-019, EXP-TURN-048, EXP-TURN-049, EXP-TURN-050, EXP-TURN-051 |
| `0x09` | 1 | `INT8` | `planned_target` | Its first target byte | established | FND-AI-019, EXP-TURN-048, EXP-TURN-049, EXP-TURN-050, EXP-TURN-051 |
| `0x0A` | 1 | `INT8` | `planned_target_2` | Its second target byte | established | FND-AI-019, EXP-TURN-048, EXP-TURN-049, EXP-TURN-050, EXP-TURN-051 |
| `0x0B` | 1 | `UINT8` | `unk_0B` | Padding: no instruction addresses it | established | FND-STATE-006, FND-AI-042, EXP-TURN-048, EXP-TURN-049, EXP-TURN-050, EXP-TURN-051 |
| `0x0C` | 2 | `INT16LE` | `weapon_cooldown` | Turns before the planner may replace the weapon; lowered by 1 each planning pass while a weapon is equipped, set to 0 otherwise | established | FND-AI-019, FND-AI-021, FND-STATE-006, EXP-TURN-048, EXP-TURN-049, EXP-TURN-050, EXP-TURN-051 |
| `0x0E` | 2 | `INT16LE` | `armor_cooldown` | The same for the armor | established | FND-AI-019, FND-AI-021, FND-STATE-006, EXP-TURN-048, EXP-TURN-049, EXP-TURN-050, EXP-TURN-051 |
| `0x10` | | | | Total size 16 | | |

## Enumerations and flags

### family

| Value | Name | Meaning | Status | Evidence |
|---|---|---|---|---|
| 99 | `FAMILY_NONE` | Stored by the reset | established | FND-STATE-006, EXP-TURN-048, EXP-TURN-049, EXP-TURN-050, EXP-TURN-051 |

## Differences between builds

None known.

## Coverage

Every record of the 486 was read from the running original at the end of
EXP-TURN-048 to EXP-TURN-051, runs of Greed, Kill 'Em All and the setup
screen's defaults, and every run recorded since that holds the block, among
them EXP-TURN-053, EXP-TURN-056, EXP-TURN-058 and EXP-TURN-074, was read the
same way. The replays compare every byte of every record with the rebuild's,
and every value agrees with the layout: families 1 to 7, 10 to 14 and 99,
action numbers of FMT-STATE-001, target bytes as the AI rules give them,
`unk_0B` 0 in every record, and cooldowns as 16-bit values, negative for gangs
that have held their weapon or armor past the cooldown and 0 for gangs without
one. The matching block of a save file was not decoded.

Of the writes to `needs_family`, the replays reach the flag for an empty or
dead roster slot, the flag for slot 0 on a player's first pass, and the Greed
Terminate branches of all seven handlers: those of families 2, 3 and 7 in
EXP-TURN-048, and those of families 1, 5, 6 and 12 in EXP-TURN-105, where the
probe writes those families into four records before the closing turns.

## Open questions

- The family values other than 99 are described by the AI rules.
- That the block is saved whole as save block 14 rests on FND-SAVE-001 and is
  FMT-SAVE-001's claim; no save the original wrote has been decoded against a
  run's memory. Every row of the layout is reached by the runs above, so the
  entry takes their status.
