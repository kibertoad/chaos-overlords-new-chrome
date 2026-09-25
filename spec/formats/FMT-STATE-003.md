---
id: FMT-STATE-003
title: Per-gang combat record of the last resolution
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: []
byte_order: little
size: 10
text: false
definition: fmt_state_003.ksy
evidence: [FND-AI-010, FND-COMBAT-004, FND-COMBAT-008, FND-COMBAT-010, FND-PLATFORM-003, FND-STATE-005]
conflicting: []
split_with: []
related: []
---

## Layout

The whole-turn resolver keeps one record per gang in the list
`combat_records`, at `0x004A11E8 + 10 * (player * 81 + roster_slot)`, 486
records in all [FND-COMBAT-004, FND-AI-010]. The resolver fills them during the
combat phase, and Detailed Combat reads them to present the fights. An attack
and the retaliation it provokes share the attacker's record; no record
describes a retaliation on its own [FND-COMBAT-004]. Each resolution sets
`police_damage` to -1 in all 486 records, then writes bytes 0 to 8 only for
the gangs that attacked, were attacked or were hit by the police in that
resolution. Every other record keeps bytes 0 to 8 from the last resolution in
which its roster slot fought, even after the gang died or the slot was
reused [FND-STATE-005]. Bytes 4 and 5 of a gang that fought without
attacking (a target, or a gang only the police found) are copied from stack
locations the combat phase never wrote, so their values are undefined
[FND-COMBAT-008].

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 1 | `UINT8` | `definition` | The gang's definition, copied from offset `0x01` of the gang record (load `0x004743DE`, store `0x004743EE`) | supported | FND-COMBAT-004, FND-COMBAT-008, FND-STATE-005 |
| `0x01` | 1 | `INT8` | `force_start` | The gang's Force at the start of the combat phase | supported | FND-COMBAT-004 |
| `0x02` | 1 | `INT8` | `force_final` | The gang's Force minus its damage for the phase capped at 10, so the Force after the combat phase, below 0 for a gang that died | supported | FND-COMBAT-004, FND-COMBAT-008 |
| `0x03` | 1 | `INT8` | `force_shown` | The Force Detailed Combat draws. Set to `force_start` once, for every gang listed in any result row, before the presentation, and reduced only by the clips shown; the resolver does not write it | supported | FND-COMBAT-004, FND-COMBAT-010, FND-STATE-005 |
| `0x04` | 1 | `INT8` | `damage_dealt` | The damage of the gang's opening attack, or -1 when the target evaded. Undefined for a gang that fought without attacking | supported | FND-COMBAT-004, FND-COMBAT-008 |
| `0x05` | 1 | `INT8` | `retaliation_taken` | The retaliation damage the gang took from its target, 0 when there was none. Undefined for a gang that fought without attacking | supported | FND-AI-010, FND-COMBAT-004, FND-COMBAT-008 |
| `0x06` | 1 | `INT8` | `weapon` | The gang's weapon item at the time of the fight, or -1 | supported | FND-COMBAT-004 |
| `0x07` | 1 | `INT8` | `armor` | The gang's armor item at the time of the fight, or -1 | supported | FND-COMBAT-004 |
| `0x08` | 1 | `INT8` | `misc` | The gang's miscellaneous item at the time of the fight, or -1 | supported | FND-COMBAT-004 |
| `0x09` | 1 | `INT8` | `police_damage` | The damage the police dealt to the gang, or -1 when the police did not find it | supported | FND-COMBAT-004, FND-COMBAT-008, FND-STATE-005 |
| `0x0A` | | | | Total size 10 | | |

## Enumerations and flags

None known.

## Differences between builds

None known.

## Coverage

A memory structure: nothing has been decoded against a dump of the running
original. The 486 records match the 4,860-byte block the save reader and writer
transfer from `0x004A11E8` [FND-PLATFORM-003], so the records of the last
resolution are saved with the game.

## Open questions

- The types are assumed signed where -1 is stored. Whether the game loads
  `definition` and the Force bytes signed is not recorded.

