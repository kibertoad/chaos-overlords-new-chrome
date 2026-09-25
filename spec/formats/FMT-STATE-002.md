---
id: FMT-STATE-002
title: Sector record, one per city sector
status: sourced
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: []
byte_order: little
size: 36
text: false
definition: fmt_state_002.ksy
evidence: [FND-AI-004, FND-AI-010, FND-CHAOS-001, FND-CONTROL-001, FND-EQUIP-001, FND-GANG-001, FND-PLATFORM-003, FND-SETUP-003, FND-TURN-001, FND-TURN-003, FND-UI-035, FND-UPKEEP-001, SRC-RECHAOS-3561D41]
conflicting: []
split_with: []
related: []
---

## Layout

The game keeps 64 of these records in the list `sectors`, one per sector in
ascending sector number, at `0x004A08E8 + sector * 0x24` [FND-CONTROL-001,
FND-UI-035]. Before each planning phase the game rebuilds the fields that come
from completed sites (`cash_yield`, `support`, the special-site flags and the
fourteen site bonuses) from the three site slots, and copies the whole record
back [FND-GANG-001, FND-UPKEEP-001, FND-UI-035].

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 1 | `INT8` | `owner` | The player slot that controls the sector, or -1 when no one does | supported | FND-CONTROL-001, FND-EQUIP-001, FND-SETUP-003, FND-UPKEEP-001 |
| `0x01` | 1 | `INT8` | `unk_01` | Purpose unknown. SRC-RECHAOS-3561D41 calls it the sector's income | sourced | SRC-RECHAOS-3561D41 |
| `0x02` | 1 | `INT8` | `unk_02` | Purpose unknown. SRC-RECHAOS-3561D41 calls it the base tolerance | sourced | SRC-RECHAOS-3561D41 |
| `0x03` | 1 | `INT8` | `cash_yield` | The cash the owner collects from the sector at each Upkeep: set to 1 by the refresh before planning, plus the Cash of each completed site. Shown to the owner only, on the row the panel labels Cash | supported | FND-UI-035, FND-UPKEEP-001 |
| `0x04` | 1 | `INT8` | `income` | The sector's Income, 3 to 7, from city generation. The refresh before planning leaves it unchanged. Shown on the Income row and read by the Chaos pool | supported | FND-CHAOS-001, FND-UI-035 |
| `0x05` | 1 | `INT8` | `tolerance` | The sector's Tolerance, shown on the Tolerance row | supported | FND-AI-004, FND-UI-035 |
| `0x06` | 1 | `INT8` | `support` | The Support of the sector's completed sites, shown on the Support row | supported | FND-GANG-001, FND-UI-035 |
| `0x07` | 6 | `FMT-STATE-004[3]` | `sites` | The sector's three site slots | supported | FND-TURN-001, FND-TURN-003 |
| `0x0D` | 1 | `UINT8` | `unk_0D` | Purpose unknown. SRC-RECHAOS-3561D41 calls it a flag for a completed research site | sourced | SRC-RECHAOS-3561D41 |
| `0x0E` | 1 | `UINT8` | `factory` | Nonzero when the sector has a completed Factory, which lowers item prices for the owner | supported | FND-EQUIP-001 |
| `0x0F` | 1 | `INT8` | `crackdown_turns` | Police presence: the number of police Combat phases left, 0 for none, 100 for a Crackdown that never ends | supported | FND-SETUP-003 |
| `0x10` | 1 | `UINT8` | `unk_10` | Purpose unknown | sourced | SRC-RECHAOS-3561D41 |
| `0x11` | 1 | `UINT8` | `unk_11` | Purpose unknown | sourced | SRC-RECHAOS-3561D41 |
| `0x12` | 1 | `UINT8` | `unk_12` | Purpose unknown | sourced | SRC-RECHAOS-3561D41 |
| `0x13` | 1 | `UINT8` | `unk_13` | Purpose unknown | sourced | SRC-RECHAOS-3561D41 |
| `0x14` | 1 | `UINT8` | `unk_14` | Purpose unknown | sourced | SRC-RECHAOS-3561D41 |
| `0x15` | 1 | `UINT8` | `unk_15` | Purpose unknown | sourced | SRC-RECHAOS-3561D41 |
| `0x16` | 1 | `INT8` | `site_combat` | Sum of the Combat modifiers of the completed sites | sourced | SRC-RECHAOS-3561D41 |
| `0x17` | 1 | `INT8` | `site_defense` | Sum of the Defense modifiers of the completed sites | sourced | SRC-RECHAOS-3561D41 |
| `0x18` | 1 | `INT8` | `site_stealth` | Sum of the Stealth modifiers of the completed sites | sourced | SRC-RECHAOS-3561D41 |
| `0x19` | 1 | `INT8` | `site_detect` | Sum of the Detect modifiers of the completed sites | sourced | SRC-RECHAOS-3561D41 |
| `0x1A` | 1 | `INT8` | `site_chaos` | Sum of the Chaos modifiers of the completed sites | sourced | SRC-RECHAOS-3561D41 |
| `0x1B` | 1 | `INT8` | `site_control` | Sum of the Control modifiers of the completed sites | sourced | SRC-RECHAOS-3561D41 |
| `0x1C` | 1 | `INT8` | `site_heal` | Sum of the Heal modifiers of the completed sites | sourced | SRC-RECHAOS-3561D41 |
| `0x1D` | 1 | `INT8` | `site_influence` | Sum of the Influence modifiers of the completed sites | sourced | SRC-RECHAOS-3561D41 |
| `0x1E` | 1 | `INT8` | `site_research` | Sum of the Research modifiers of the completed sites | sourced | SRC-RECHAOS-3561D41 |
| `0x1F` | 1 | `INT8` | `site_strength` | Sum of the Strength modifiers of the completed sites | sourced | SRC-RECHAOS-3561D41 |
| `0x20` | 1 | `INT8` | `site_blade` | Sum of the Blade modifiers of the completed sites | sourced | SRC-RECHAOS-3561D41 |
| `0x21` | 1 | `INT8` | `site_ranged` | Sum of the Ranged modifiers of the completed sites | sourced | SRC-RECHAOS-3561D41 |
| `0x22` | 1 | `INT8` | `site_fighting` | Sum of the Fighting modifiers of the completed sites | sourced | SRC-RECHAOS-3561D41 |
| `0x23` | 1 | `INT8` | `site_martial_arts` | Sum of the Martial Arts modifiers of the completed sites | sourced | SRC-RECHAOS-3561D41 |
| `0x24` | | | | Total size 36 | | |

## Enumerations and flags

### owner

| Value | Name | Meaning | Status | Evidence |
|---|---|---|---|---|
| -1 | `SECTOR_NEUTRAL` | No player controls the sector | supported | FND-CONTROL-001, FND-SETUP-003 |

### crackdown_turns

| Value | Name | Meaning | Status | Evidence |
|---|---|---|---|---|
| 100 | `CRACKDOWN_PERMANENT` | A Crackdown that the end-of-turn decrement never reduces | supported | FND-SETUP-003 |

## Differences between builds

None known.

## Coverage

A memory structure: nothing has been decoded against a dump of the running
original. The record size and count agree with the 2,304-byte block the save
reader and writer transfer from `0x004A08E8` [FND-PLATFORM-003].

## Open questions

- Which byte the Control pass reads, and which byte case 6 of the computer
  players' sector selector `fn_00402D70` reads. FND-CONTROL-001 does not give
  the instruction, and the earlier claim that both read `cash_yield` was an
  interpretation (FND-UPKEEP-001, Alternatives).
- Where city generation writes the generated Income and the starting Tolerance
  of 17 minus Income, and whether `unk_01` and `unk_02` hold them.
- Whether the refresh before planning recomputes `tolerance` from a base value and the
  completed sites' Tolerance, or leaves it alone. Bribe and Snitch change the
  sector's Tolerance byte directly; the findings do not give its offset, and
  `tolerance` at `0x05` is assumed to be that byte.
- The offsets of the fourteen site bonuses, and the order of the statistics
  among them, come from SRC-RECHAOS-3561D41 only. FND-GANG-001 shows that the
  record holds fourteen such sums.
- The purposes of `unk_01`, `unk_02`, `unk_0D` and `unk_10` to `unk_15`.
- The sector index 64 lies past the end of the list. Reads there alias the
  first bytes of `combat_records` (FMT-STATE-003) [FND-AI-010].
