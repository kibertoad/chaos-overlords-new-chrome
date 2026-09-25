---
id: FMT-STATE-007
title: Computer player planning record, one per player and roster slot
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: []
byte_order: little
size: 16
text: false
definition: fmt_state_007.ksy
evidence: [FND-AI-019, FND-AI-021, FND-SAVE-001, FND-STATE-006, FND-AI-042]
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
| `0x00` | 1 | `INT8` | `family` | The planning family the record's gang follows; 99 after a reset | supported | FND-AI-019, FND-STATE-006 |
| `0x01` | 1 | `UINT8` | `needs_family` | Set to 1 when the record needs a new family: for a slot whose gang is not alive at a planning pass, for slot 0 on a player's first pass, and by the Greed Terminate branches of seven family handlers; cleared by the reset. Read only by selector `0x48`, which makes the dispatcher reset the record and assign a family | supported | FND-STATE-006, FND-AI-042 |
| `0x02` | 1 | `UINT8` | `older_action` | The action planned two turns ago, a command number of FMT-STATE-001 `action` | supported | FND-AI-019 |
| `0x03` | 1 | `INT8` | `older_target` | Its first target byte | supported | FND-AI-019 |
| `0x04` | 1 | `INT8` | `older_target_2` | Its second target byte | supported | FND-AI-019 |
| `0x05` | 1 | `UINT8` | `previous_action` | The action planned last turn | supported | FND-AI-019 |
| `0x06` | 1 | `INT8` | `previous_target` | Its first target byte | supported | FND-AI-019 |
| `0x07` | 1 | `INT8` | `previous_target_2` | Its second target byte | supported | FND-AI-019 |
| `0x08` | 1 | `UINT8` | `planned_action` | The action planned this turn | supported | FND-AI-019 |
| `0x09` | 1 | `INT8` | `planned_target` | Its first target byte | supported | FND-AI-019 |
| `0x0A` | 1 | `INT8` | `planned_target_2` | Its second target byte | supported | FND-AI-019 |
| `0x0B` | 1 | `UINT8` | `unk_0B` | Padding: no instruction addresses it | supported | FND-STATE-006, FND-AI-042 |
| `0x0C` | 2 | `INT16LE` | `weapon_cooldown` | Turns before the planner may replace the weapon; lowered by 1 each planning pass while a weapon is equipped, set to 0 otherwise | supported | FND-AI-019, FND-AI-021, FND-STATE-006 |
| `0x0E` | 2 | `INT16LE` | `armor_cooldown` | The same for the armor | supported | FND-AI-019, FND-AI-021, FND-STATE-006 |
| `0x10` | | | | Total size 16 | | |

## Enumerations and flags

### family

| Value | Name | Meaning | Status | Evidence |
|---|---|---|---|---|
| 99 | `FAMILY_NONE` | Stored by the reset | supported | FND-STATE-006 |

## Differences between builds

None known.

## Coverage

A memory structure: nothing has been decoded against a dump of the running
original or against the matching block of a save file.

## Open questions

- The family values other than 99 are described by the AI rules.
