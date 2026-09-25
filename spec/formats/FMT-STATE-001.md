---
id: FMT-STATE-001
title: Gang record, one per player and roster slot
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: []
byte_order: little
size: 32
text: false
definition: fmt_state_001.ksy
evidence: [FND-AI-004, FND-AI-007, FND-CHAOS-001, FND-COMBAT-001, FND-COMBAT-004, FND-CONTROL-001, FND-DETECT-001, FND-EQUIP-002, FND-EQUIP-007, FND-EQUIP-008, FND-GANG-001, FND-GANG-003, FND-GANG-005, FND-GANG-007, FND-HIDE-001, FND-HIRE-002, FND-MOVE-001, FND-MOVE-003, FND-PLATFORM-003, FND-STATE-002, FND-TURN-001, FND-TURN-002, FND-TURN-004, FND-TURN-005, FND-UI-036, SRC-RECHAOS-3561D41]
conflicting: []
split_with: []
related: []
---

## Layout

The game keeps 486 of these records in the list `gangs`: six players of 81
roster slots each. The record for player `p` and roster slot `s` sits at
`0x00498DA8 + p * 0xA20 + s * 0x20` [FND-HIRE-002, FND-UI-036]. The resolver
copies a whole record (eight 32-bit words) out, works on the copy and copies it
back [FND-GANG-003]. Hiring copies a complete new record into a free slot
[FND-TURN-005]. The hire looks for a free slot among slots 0 to 79 only, so
slot 80 is never filled by a hire, and slot 0 can be reused once the Right
Hands are dead. The new-match setup writes slot 0 of each player, the Right
Hands, with definition 0 and Force 10 [FND-STATE-002].

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 1 | `INT8` | `player` | The owning player slot, 0 to 5 | supported | FND-STATE-002 |
| `0x01` | 1 | `UINT8` | `definition` | The gang's record number in the gang definition table read from `DATA/Gangs`; 0 for the Right Hands | supported | FND-STATE-002 |
| `0x02` | 1 | `INT8` | `sector` | The sector the gang is in, 0 to 63, or 100 (`GANG_INACTIVE`) when the slot holds no living gang. A gang dies or is terminated by this byte alone becoming 100 | supported | FND-DETECT-001, FND-GANG-003, FND-HIRE-002, FND-TURN-004, FND-TURN-005, FND-UI-036 |
| `0x03` | 1 | `INT8` | `force` | Current Force: 10 for the Right Hands, set when hired, lowered by combat damage; the gang dies when it falls below 1 | supported | FND-STATE-002, FND-GANG-005 |
| `0x04` | 1 | `INT8` | `weapon` | Item record number of the equipped weapon, or -1 for none. Kept unchanged when the gang dies or is terminated | supported | FND-AI-007, FND-GANG-001, FND-GANG-003 |
| `0x05` | 1 | `INT8` | `armor` | Item record number of the equipped armor, or -1 for none. Kept unchanged when the gang dies or is terminated | supported | FND-GANG-001, FND-GANG-003 |
| `0x06` | 1 | `INT8` | `misc` | Item record number of the equipped miscellaneous item, or -1 for none. Kept unchanged when the gang dies or is terminated | supported | FND-GANG-001, FND-GANG-003 |
| `0x07` | 1 | `UINT8` | `action` | The action the gang carries out this turn. Hide is in force while this byte is 8 | supported | FND-AI-007, FND-COMBAT-004, FND-HIDE-001, FND-TURN-002 |
| `0x08` | 1 | `INT8` | `target` | First target byte of `action`: the target player for Attack, the destination sector for Move, the item for Equip and Research, the site slot 0 to 2 for Influence, and the item mask (weapon 1, armor 2, misc 4) for Give and Sell. No other action reads it | supported | FND-COMBAT-004, FND-EQUIP-007, FND-STATE-002, FND-TURN-002, FND-EQUIP-008, FND-MOVE-003 |
| `0x09` | 1 | `INT8` | `target_2` | Second target byte of `action`: the target's roster slot for Attack, the recipient's roster slot for Give. No other action reads it | supported | FND-COMBAT-004, FND-EQUIP-007, FND-STATE-002, FND-EQUIP-008 |
| `0x0A` | 1 | `UINT8` | `repeat_action` | The recurring action, copied into `action` at the start of each turn; 0 for none | supported | FND-HIDE-001, FND-TURN-002, FND-TURN-004 |
| `0x0B` | 1 | `INT8` | `repeat_target` | The recurring action's target, copied into `target` with it | supported | FND-TURN-002, FND-TURN-004 |
| `0x0C` | 6 | `UINT8[6]` | `visible_to` | One byte per observing player slot: nonzero when that player can see this gang. Rebuilt by the visibility pass; always nonzero for the owner | supported | FND-DETECT-001, FND-UI-036 |
| `0x12` | 1 | `INT8` | `combat` | Effective Combat, including the combat skills that fit the weapon (see below) | supported | FND-AI-004, FND-GANG-001, FND-STATE-002, FND-GANG-007 |
| `0x13` | 1 | `INT8` | `defense` | Effective Defense | supported | FND-AI-004, FND-GANG-001 |
| `0x14` | 1 | `INT8` | `stealth` | Effective Stealth | supported | FND-DETECT-001, FND-GANG-001 |
| `0x15` | 1 | `INT8` | `detect` | Effective Detect | supported | FND-DETECT-001, FND-GANG-001 |
| `0x16` | 1 | `INT8` | `chaos` | Effective Chaos | supported | FND-STATE-002 |
| `0x17` | 1 | `INT8` | `control` | Effective Control | supported | FND-AI-004, FND-GANG-001 |
| `0x18` | 1 | `INT8` | `heal` | Effective Heal | supported | FND-AI-004, FND-GANG-001 |
| `0x19` | 1 | `INT8` | `influence` | Effective Influence | supported | FND-STATE-002 |
| `0x1A` | 1 | `INT8` | `research` | Effective Research | supported | FND-STATE-002 |
| `0x1B` | 1 | `INT8` | `strength` | Effective Strength | supported | FND-STATE-002 |
| `0x1C` | 1 | `INT8` | `blade` | Effective Blade | supported | FND-STATE-002 |
| `0x1D` | 1 | `INT8` | `ranged` | Effective Ranged | supported | FND-STATE-002 |
| `0x1E` | 1 | `INT8` | `fighting` | Effective Fighting | supported | FND-STATE-002 |
| `0x1F` | 1 | `INT8` | `martial_arts` | Effective Martial Arts, the last of the fourteen effective statistics | supported | FND-AI-007, FND-GANG-001 |
| `0x20` | | | | Total size 32 | | |

The fourteen effective statistics at `0x12..0x20` are rebuilt before planning:
each starts from the same statistic of the gang's definition, adds that
statistic of each equipped item that is not -1, and adds the sector's site
bonus when the gang's player owns the gang's sector [FND-GANG-001]. The
definition, item and site tables list the statistics in the same order as this
record, the definition's Tech Level field aside. Combat is built last and
adds, by the weapon's item type, Strength, Fighting and Martial Arts when the
gang has no weapon, Strength for type 0, Strength and Blade for type 1, Ranged
for type 2, and nothing for any other type. The resolver reads Chaos, Control,
Heal, Influence and Research for the actions of those names [FND-STATE-002].

## Enumerations and flags

### action, repeat_action

| Value | Name | Meaning | Status | Evidence |
|---|---|---|---|---|
| 0 | `ACTION_NONE` | No action | supported | FND-TURN-002 |
| 1 | `ACTION_ATTACK` | Attack | supported | FND-COMBAT-001, FND-COMBAT-004 |
| 2 | `ACTION_BRIBE` | Bribe | supported | FND-TURN-001, FND-TURN-002 |
| 3 | `ACTION_CHAOS` | Chaos | supported | FND-CHAOS-001, FND-TURN-002 |
| 4 | `ACTION_CONTROL` | Control | supported | FND-CONTROL-001, FND-TURN-002 |
| 5 | `ACTION_EQUIP` | Equip | supported | FND-EQUIP-002 |
| 6 | `ACTION_GIVE` | Give | supported | FND-EQUIP-002 |
| 7 | `ACTION_HEAL` | Heal | supported | FND-TURN-001, FND-TURN-002 |
| 8 | `ACTION_HIDE` | Hide | supported | FND-HIDE-001, FND-TURN-001 |
| 9 | `ACTION_INFLUENCE` | Influence | supported | FND-TURN-001, FND-TURN-002 |
| 10 | `ACTION_MOVE` | Move | supported | FND-MOVE-001 |
| 11 | `ACTION_RESEARCH` | Research | supported | FND-TURN-001, FND-TURN-002 |
| 12 | `ACTION_SELL` | Sell | supported | FND-EQUIP-002 |
| 13 | `ACTION_SNITCH` | Snitch | supported | FND-TURN-001, FND-TURN-002 |
| 14 | `ACTION_TERMINATE` | Terminate | supported | FND-MOVE-001 |

### sector

| Value | Name | Meaning | Status | Evidence |
|---|---|---|---|---|
| 100 | `GANG_INACTIVE` | The roster slot holds no living gang; a hire may reuse it | supported | FND-GANG-003, FND-TURN-004, FND-TURN-005 |

## Differences between builds

None known.

## Coverage

A memory structure: nothing has been decoded against a dump of the running
original. The record size and the 486-record block agree with the
15,552-byte block the save reader and writer transfer from `0x00498DA8`
[FND-PLATFORM-003].

## Open questions

- The names of `strength`, `blade`, `ranged` and `fighting` follow the
  definition table's field names (FMT-DATA-002), which rest on an outside
  source. The executable fixes their order and which of them Combat adds for
  each weapon type [FND-STATE-002].
- Whether any byte of `visible_to` other than 0 and 1 is ever stored is not
  recorded.
