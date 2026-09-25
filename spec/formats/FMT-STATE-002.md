---
id: FMT-STATE-002
title: Sector record, one per city sector
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: []
byte_order: little
size: 36
text: false
definition: fmt_state_002.ksy
evidence: [FND-CHAOS-002, FND-AI-004, FND-AI-010, FND-CHAOS-001, FND-CONTROL-001, FND-CONTROL-003, FND-EQUIP-001, FND-EQUIP-007, FND-EQUIP-008, FND-GANG-001, FND-GANG-007, FND-HIRE-008, FND-PLATFORM-003, FND-SETUP-003, FND-STATE-001, FND-STATE-002, FND-TOLERANCE-001, FND-TURN-001, FND-TURN-003, FND-UI-015, FND-UI-018, FND-UI-035, FND-UPKEEP-001, FND-UPKEEP-002]
conflicting: []
split_with: []
related: []
---

## Layout

The game keeps 64 of these records in the list `sectors`, one per sector in
ascending sector number, at `0x004A08E8 + sector * 0x24` [FND-CONTROL-001,
FND-UI-035]. City generation writes `base_income`, `base_tolerance` and the
site slots [FND-STATE-001]. Before each planning phase the game rebuilds
`cash_yield`, `income`, `tolerance`, `support`, `research_level`, `factory`
and the fourteen site bonuses from the base values and the three site slots,
and copies the whole record back [FND-GANG-001, FND-STATE-001, FND-UPKEEP-001,
FND-UI-035]. A site slot counts as completed when its progress is at least
its definition's Resistance [FND-STATE-001].

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 1 | `INT8` | `owner` | The player slot that controls the sector, or -1 when no one does | supported | FND-CONTROL-001, FND-EQUIP-001, FND-SETUP-003, FND-UPKEEP-001 |
| `0x01` | 1 | `INT8` | `base_income` | The sector's Income from city generation, 3 to 7. Nothing writes it after generation | supported | FND-STATE-001 |
| `0x02` | 1 | `INT8` | `base_tolerance` | The sector's Tolerance before its sites: 17 minus `base_income` at generation, then changed by Bribe (+3), Snitch (-3), a step of one toward 17 minus `base_income` at the start of each resolution, and a clamp to 1..40 after the instant phase. Bribe and Snitch add in 32 bits and store the low byte | supported | FND-STATE-001, FND-TOLERANCE-001 |
| `0x03` | 1 | `INT8` | `cash_yield` | The cash the owner collects from the sector at each Upkeep: set to 1 by the refresh before planning, plus the Cash of each completed site. Shown to the owner only, on the row the panel labels Cash | supported | FND-STATE-001, FND-UI-035, FND-UPKEEP-001, FND-UPKEEP-002 |
| `0x04` | 1 | `INT8` | `income` | The sector's Income: the refresh before planning copies `base_income` here. Shown on the Income row and read by the Chaos pool (load `0x004732A7`) and the Control pass | supported | FND-CHAOS-001, FND-CHAOS-002, FND-STATE-001, FND-UI-035, FND-CONTROL-003 |
| `0x05` | 1 | `INT8` | `tolerance` | The sector's Tolerance: the refresh before planning sets it to `base_tolerance` plus the Tolerance of each completed site. Shown on the Tolerance row. Changes to `base_tolerance` during a resolution reach it only at the next refresh. The Chaos sector pass compares with this byte | supported | FND-AI-004, FND-STATE-001, FND-TOLERANCE-001, FND-UI-035 |
| `0x06` | 1 | `INT8` | `support` | The Support of the sector's completed sites, shown on the Support row: set to 0 by the refresh, plus the Support of each completed site. Read by the Control pass | supported | FND-GANG-001, FND-STATE-001, FND-UI-035, FND-CONTROL-003 |
| `0x07` | 6 | `FMT-STATE-004[3]` | `sites` | The sector's three site slots | supported | FND-TURN-001, FND-TURN-003 |
| `0x0D` | 1 | `UINT8` | `research_level` | 0, raised to 1 by a completed site whose `special` is 1 and to 2 by one whose `special` is 2 (the higher wins). The item list for a gang whose player owns the sector caps the items' Tech Level at 5 for 0 and at 8 for 1 | supported | FND-STATE-001 |
| `0x0E` | 1 | `UINT8` | `factory` | 1 when a completed site has `special` 3, else 0; set only by the refresh before planning. Lowers item prices for the owner | supported | FND-EQUIP-001, FND-STATE-001, FND-EQUIP-007, FND-EQUIP-008 |
| `0x0F` | 1 | `INT8` | `crackdown_turns` | Police presence: the number of police Combat phases left, 0 for none, 100 for a Crackdown that never ends. The Control pass settles no sector where it is not 0 | supported | FND-SETUP-003, FND-CONTROL-003 |
| `0x10` | 6 | `UINT8[6]` | `gangs_seen` | One byte per player slot: 1 when that player has a living gang in the sector that the player at the screen can see, else 0. Rebuilt by the city map drawer, not by resolution; read by the map, the panels, the Attack picker and the hire drop test. The Overlord bar lights the portraits of the players whose byte is set, and the sector view offers their gangs (FND-UI-015, FND-UI-018) | supported | FND-HIRE-008, FND-STATE-001, FND-UI-015, FND-UI-018 |
| `0x16` | 1 | `INT8` | `site_combat` | Sum of the Combat modifiers of the completed sites | supported | FND-STATE-001, FND-STATE-002 |
| `0x17` | 1 | `INT8` | `site_defense` | Sum of the Defense modifiers of the completed sites | supported | FND-STATE-001, FND-STATE-002 |
| `0x18` | 1 | `INT8` | `site_stealth` | Sum of the Stealth modifiers of the completed sites | supported | FND-STATE-001, FND-STATE-002 |
| `0x19` | 1 | `INT8` | `site_detect` | Sum of the Detect modifiers of the completed sites | supported | FND-STATE-001, FND-STATE-002 |
| `0x1A` | 1 | `INT8` | `site_chaos` | Sum of the Chaos modifiers of the completed sites | supported | FND-STATE-001, FND-STATE-002 |
| `0x1B` | 1 | `INT8` | `site_control` | Sum of the Control modifiers of the completed sites | supported | FND-STATE-001, FND-STATE-002 |
| `0x1C` | 1 | `INT8` | `site_heal` | Sum of the Heal modifiers of the completed sites | supported | FND-STATE-001, FND-STATE-002 |
| `0x1D` | 1 | `INT8` | `site_influence` | Sum of the Influence modifiers of the completed sites | supported | FND-STATE-001, FND-STATE-002 |
| `0x1E` | 1 | `INT8` | `site_research` | Sum of the Research modifiers of the completed sites | supported | FND-STATE-001, FND-STATE-002 |
| `0x1F` | 1 | `INT8` | `site_strength` | Sum of the Strength modifiers of the completed sites | supported | FND-STATE-001, FND-STATE-002 |
| `0x20` | 1 | `INT8` | `site_blade` | Sum of the Blade modifiers of the completed sites | supported | FND-STATE-001, FND-STATE-002 |
| `0x21` | 1 | `INT8` | `site_ranged` | Sum of the Ranged modifiers of the completed sites | supported | FND-STATE-001, FND-STATE-002 |
| `0x22` | 1 | `INT8` | `site_fighting` | Sum of the Fighting modifiers of the completed sites | supported | FND-STATE-001, FND-STATE-002 |
| `0x23` | 1 | `INT8` | `site_martial_arts` | Sum of the Martial Arts modifiers of the completed sites | supported | FND-STATE-001, FND-STATE-002 |
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

- The statistic names of the fourteen site bonuses follow the site
  definition's field names (FMT-DATA-001), which rest on an outside source.
  The executable fixes that site field `0x20 + 2k` feeds byte `0x16 + k`,
  which feeds gang byte `0x12 + k` (FMT-STATE-001) [FND-STATE-001,
  FND-STATE-002].
- The sector index 64 lies past the end of the list. Reads there alias the
  first bytes of `combat_records` (FMT-STATE-003) [FND-AI-010].
