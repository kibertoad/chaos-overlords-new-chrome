---
id: FMT-DATA-003
title: Item definition records in DATA/ITEMS
status: sourced
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: ["DATA/ITEMS"]
byte_order: little
size: 166
text: false
definition: fmt_data_003.ksy
evidence: [FND-DATA-006, FND-DATA-007, FND-COMBAT-010, FND-GANG-007, FND-DATA-003, FND-ASSET-001, FND-RESEARCH-002, FND-GANG-001, FND-EQUIP-001, FND-EQUIP-002, FND-AUDIO-002, FND-AUDIO-013, SRC-RECHAOS-3561D41]
conflicting: []
split_with: []
related: []
---

## Layout

`DATA/ITEMS` holds 64 of these records back to back, record `n` at offset
`n * 166` (FND-DATA-003). Records 0 to 52 define the 53 items; records 53 to 63
are blank, with `type` 99 and every other number 0. The executable opens the
file as `data\Items` (FND-ASSET-001) and walks all 64 records where it scans the
table (FND-RESEARCH-002). Every number is a signed 16-bit little-endian integer.

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 30 | `char[30]` | `name` | The item's name. ASCII text, then one NUL byte, then spaces to the end of the field; 30 spaces in a blank record. Read it as a C string. | sourced | FND-DATA-003, SRC-RECHAOS-3561D41 |
| `0x1E` | 2 | `INT16LE` | `id` | The item's number, equal to the record's index in records 0 to 52, and 0 in the blank records. | supported | FND-DATA-003 |
| `0x20` | 90 | `char[90]` | `description` | The item's description. ASCII text padded with spaces to the end of the field, with no NUL byte. | sourced | FND-DATA-003, SRC-RECHAOS-3561D41 |
| `0x7A` | 2 | `INT16LE` | `type` | The item's category, one of the values below. The statistics rebuild reads it for the weapon to choose the skills added to Combat. | supported | FND-DATA-003, FND-GANG-007, SRC-RECHAOS-3561D41 |
| `0x7C` | 2 | `INT16LE` | `research_difficulty` | Research needed to complete the item. A new game copies its low byte into each player's research progress for the item; 0 means researched from the start. | supported | FND-DATA-003, FND-RESEARCH-002 |
| `0x7E` | 2 | `INT16LE` | `cost` | Purchase price in dollars, 0 to 45. | sourced | FND-EQUIP-001, FND-EQUIP-002, SRC-RECHAOS-3561D41 |
| `0x80` | 2 | `INT16LE` | `tech_level` | Tech Level of the item, 0 to 10. | sourced | FND-DATA-003, SRC-RECHAOS-3561D41 |
| `0x82` | 2 | `INT16LE` | `combat` | Modifier to the Combat of the gang that carries the item. | supported | FND-GANG-001, FND-GANG-007, SRC-RECHAOS-3561D41 |
| `0x84` | 2 | `INT16LE` | `defense` | Modifier to Defense, applied the same way as `combat`. | supported | FND-GANG-001, FND-GANG-007, SRC-RECHAOS-3561D41 |
| `0x86` | 2 | `INT16LE` | `stealth` | Modifier to Stealth, applied the same way as `combat`. | supported | FND-GANG-001, FND-GANG-007, SRC-RECHAOS-3561D41 |
| `0x88` | 2 | `INT16LE` | `detect` | Modifier to Detect, applied the same way as `combat`. | supported | FND-GANG-001, FND-GANG-007, SRC-RECHAOS-3561D41 |
| `0x8A` | 2 | `INT16LE` | `chaos` | Modifier to Chaos, applied the same way as `combat`. | supported | FND-GANG-001, FND-GANG-007, SRC-RECHAOS-3561D41 |
| `0x8C` | 2 | `INT16LE` | `control` | Modifier to Control, applied the same way as `combat`. | supported | FND-GANG-001, FND-GANG-007, SRC-RECHAOS-3561D41 |
| `0x8E` | 2 | `INT16LE` | `heal` | Modifier to Heal, applied the same way as `combat`. | supported | FND-GANG-001, FND-GANG-007, SRC-RECHAOS-3561D41 |
| `0x90` | 2 | `INT16LE` | `influence` | Modifier to Influence, applied the same way as `combat`. | supported | FND-GANG-001, FND-GANG-007, SRC-RECHAOS-3561D41 |
| `0x92` | 2 | `INT16LE` | `research` | Modifier to Research, applied the same way as `combat`. | supported | FND-GANG-001, FND-GANG-007, SRC-RECHAOS-3561D41 |
| `0x94` | 2 | `INT16LE` | `strength` | Modifier to Strength, applied the same way as `combat`. | supported | FND-GANG-001, FND-GANG-007, SRC-RECHAOS-3561D41 |
| `0x96` | 2 | `INT16LE` | `blade` | Modifier to Blade, applied the same way as `combat`. 0 in every record. | supported | FND-DATA-003, FND-GANG-001, FND-GANG-007, SRC-RECHAOS-3561D41 |
| `0x98` | 2 | `INT16LE` | `range` | Modifier to Range, applied the same way as `combat`. | supported | FND-GANG-001, FND-GANG-007, SRC-RECHAOS-3561D41 |
| `0x9A` | 2 | `INT16LE` | `fighting` | Modifier to Fighting, applied the same way as `combat`. 0 in every record. | supported | FND-DATA-003, FND-GANG-001, FND-GANG-007, SRC-RECHAOS-3561D41 |
| `0x9C` | 2 | `INT16LE` | `martial_arts` | Modifier to Martial Arts, applied the same way as `combat`. 0 in every record. | supported | FND-DATA-003, FND-GANG-001, FND-GANG-007, SRC-RECHAOS-3561D41 |
| `0x9E` | 2 | `INT16LE` | `attack_animation` | Number of the attacker's animation strip, `PX07000` plus this value in `DATA/PX16/` or `DATA/PX08/`, 0 to 26. | supported | FND-DATA-007, FND-COMBAT-010, FND-DATA-003, SRC-RECHAOS-3561D41 |
| `0xA0` | 2 | `INT16LE` | `hit_animation` | Number of the target's animation strip, `PX07100` plus this value in `DATA/PX16/` or `DATA/PX08/`, 0 to 19. | supported | FND-DATA-007, FND-COMBAT-010, FND-DATA-003, SRC-RECHAOS-3561D41 |
| `0xA2` | 2 | `INT16LE` | `sound` | Attack sound: an attack with the item loads `DATA/SND00500` plus this value, 0 to 17. | supported | FND-DATA-007, FND-COMBAT-010, FND-DATA-003, FND-AUDIO-013, SRC-RECHAOS-3561D41 |
| `0xA4` | 2 | `INT16LE` | `combat_portrait_frame` | Index of the 48-by-48 frame, 0 to 14, that Detailed Combat copies from the item's `PX04xxx` rotation strip. Not read anywhere else. | supported | FND-DATA-003, FND-AUDIO-013 |
| `0xA6` | | | | Total size 166 | | |

## Enumerations and flags

### `type`

| Value | Name | Meaning | Status | Evidence |
|---|---|---|---|---|
| 0 | `ITEM_TYPE_MELEE` | A melee weapon; as the weapon, adds Strength to Combat. | supported | FND-DATA-003, FND-GANG-007, SRC-RECHAOS-3561D41 |
| 1 | `ITEM_TYPE_BLADE` | A bladed weapon; as the weapon, adds Strength and Blade to Combat. | supported | FND-DATA-003, FND-GANG-007, SRC-RECHAOS-3561D41 |
| 2 | `ITEM_TYPE_RANGED` | A ranged weapon; as the weapon, adds Ranged to Combat. | supported | FND-DATA-003, FND-GANG-007, SRC-RECHAOS-3561D41 |
| 3 | `ITEM_TYPE_ARMOR` | Armor. | sourced | FND-DATA-003, SRC-RECHAOS-3561D41 |
| 4 | `ITEM_TYPE_MISC` | A miscellaneous item. | sourced | FND-DATA-003, SRC-RECHAOS-3561D41 |
| 99 | `ITEM_TYPE_UNDEFINED` | A blank record that holds no item. | supported | FND-DATA-003, FND-RESEARCH-002 |

## Differences between builds

None known.

## Coverage

`DATA/ITEMS` of BLD-GOG-EN-1.1 (10,624 bytes) was read with a script as 64
records of this layout (FND-DATA-003). Every byte falls in a field, records 0
to 52 carry their own index in `id`, the 11 blank records hold spaces, 99 and
zeros, and every `type` value is in the table above. The Kaitai definition
compiles and parses the file (FND-DATA-006). The executable loads the whole
file into `0x004A5F08` and reads the fields at these offsets (FND-DATA-007).

## Open questions

- The executable reads `cost` in 19 functions and `tech_level` in four
  (FND-DATA-007), but what those reads do has not been followed, so the two
  rows rest on the source's names and on the value ranges.
  The statistics rebuild reads the fourteen modifiers and `type`
  [FND-GANG-007], and Detailed Combat reads `attack_animation`,
  `hit_animation` and `sound` [FND-COMBAT-010].
- The source numbers the file as 160 records; the file holds 64
  (SRC-RECHAOS-3561D41, Known errors).
- Which equipment slot each `type` fills (weapon for 0 to 2, armor for 3,
  miscellaneous for 4) follows from the names and has not been traced in the
  Equip resolver.
