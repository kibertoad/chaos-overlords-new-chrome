---
id: FMT-STATE-008
title: Combat result row of one sector
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: []
byte_order: little
size: 150
text: false
definition: fmt_state_008.ksy
evidence: [FND-COMBAT-007, FND-COMBAT-008, FND-COMBAT-010, FND-COMBAT-012, FND-SAVE-001]
conflicting: []
split_with: []
related: [RULE-COMBAT-002, RULE-COMBAT-004]
---

## Layout

The game keeps one row per sector in the list `combat_results`, at
`0x004A8888 + sector * 0x96`, 64 rows in ascending sector order
[FND-COMBAT-008]. The 9,600-byte table is block 25 of a save file
[FND-SAVE-001]. The resolver fills it in the combat phase; Combat Results and
Detailed Combat read it [FND-COMBAT-007, FND-COMBAT-010, FND-COMBAT-012].

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 144 | `entry[6][6]` | `players` | Six rows, one per player slot in slot order, of six four-byte entries: the player's gangs that fought in the sector, in roster order | supported | FND-COMBAT-008 |
| `0x90` | 6 | `UINT8[6]` | `police_hit` | Per player slot, 1 when the police found one of the player's gangs in the sector this phase, else 0 | supported | FND-COMBAT-008 |
| `0x96` | | | | Total size 150 | | |

Each entry:

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 2 | `INT16LE` | `gang` | The gang's element number, `player * 81 + roster_slot`, or -1 for an empty entry | supported | FND-COMBAT-008 |
| `0x02` | 2 | `INT16LE` | `target` | The element number of the gang's Attack target, `target * 81 + target_2`, or -1 when the gang's action was not Attack | supported | FND-COMBAT-008 |
| `0x04` | | | | Total size 4 | | |

## Enumerations and flags

None known.

## Differences between builds

None known.

## Coverage

A memory structure: nothing has been decoded against a dump of the running
original. The size agrees with the 9,600-byte block the save reader and
writer transfer from `0x004A8888` [FND-SAVE-001].

## Open questions

- Each combat phase sets `gang` of all 36 entries to -1 and leaves `target`
  as it was, so an empty entry can hold a stale `target`; the readers test
  `gang` first [FND-COMBAT-008, FND-COMBAT-010].
- The fill does not bound the entry counter at six. More than six fighting
  gangs of one player in one sector would write into the next player's row;
  whether the rules ever allow that is not recorded.
