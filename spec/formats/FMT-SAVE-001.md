---
id: FMT-SAVE-001
title: Full save file
status: sourced
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: []
byte_order: little
size: null
text: false
definition: fmt_save_001.ksy
evidence: [FND-SAVE-001, SRC-RECHAOS-3561D41, FND-STATE-003, FND-AI-041, FND-AI-042, FND-AI-043, FND-SETUP-015, FND-AI-012, FND-AI-001, FND-AI-002, FND-AI-004, FND-UI-003, FND-TURN-005, FND-AI-005, FND-AI-006, FND-AI-007, FND-AI-009, FND-AI-010, FND-AI-019, FND-AUDIO-011, FND-AWARDS-001, FND-AWARDS-002, FND-COMBAT-003, FND-COMBAT-004, FND-CONTROL-001, FND-EVENT-001, FND-HIDE-001, FND-HIRE-001, FND-HIRE-004, FND-HIRE-005, FND-POLICE-001, FND-RESEARCH-001, FND-RESEARCH-002, FND-SETUP-001, FND-SETUP-002, FND-SETUP-009, FND-SETUP-011, FND-TURN-002, FND-UPKEEP-001, FND-PLATFORM-003]
conflicting: []
split_with: []
related: []
---

## Layout

A full save is the opening marker, 44 blocks copied byte for byte from the
game's globals in a fixed order with no padding, an extra block in the network
form, and the marker again (FND-SAVE-001). The load function reads it with
the same list of calls through the file wrappers (FND-PLATFORM-003). A marker
of `M10W` makes a different, 16-byte file, described in FMT-SAVE-002.

Each row names the global the block is copied from. The meaning comes from
the finding that identifies that global, or from the source's save table
where no finding does; the source was made from another executable, so its
names are leads for this build. The Status column rates the whole row.

The game ships no save files, so `files` is empty.

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 4 | `UINT32LE` | `magic` | The opening marker, `SAVE_MAGIC_S40W` or `SAVE_MAGIC_N40W`. | supported | FND-SAVE-001 |
| `0x04` | 15552 | `FMT-STATE-001[486]` | `gangs` | Block 1, a copy of `0x00498DA8`. The list `gangs`: six players of 81 roster slots. | supported | FND-SAVE-001, FND-TURN-002, FND-HIDE-001 |
| `0x3CC4` | 2304 | `FMT-STATE-002[64]` | `sectors` | Block 2, a copy of `0x004A08E8`. The 64 sector records. | supported | FND-SAVE-001, FND-UPKEEP-001, FND-CONTROL-001 |
| `0x45C4` | 24 | `INT32LE[6]` | `cursor_sectors` | Block 3, a copy of `0x004ABBF0`. Per player, the sector the cursor was on. | sourced | FND-SAVE-001, SRC-RECHAOS-3561D41 |
| `0x45DC` | 6 | `UINT8[6]` | `portraits` | Block 4, a copy of `0x004A5F00`. Per player, the portrait number. | sourced | FND-SAVE-001, SRC-RECHAOS-3561D41 |
| `0x45E2` | 24 | `INT32LE[6]` | `controller` | Block 5, a copy of `0x004AB638`. The glossary's `controller`: per player slot, -1 empty, 0 human at this computer, 1 computer, 3 human over the network. | supported | FND-SAVE-001, FND-SETUP-002, FND-AI-004, FND-TURN-005 |
| `0x45FA` | 4 | `INT32LE` | `elapsed_turns` | Block 6, a copy of `0x0049CA68`. The glossary's `elapsed_turns`: turns completed, from 0. | supported | FND-SAVE-001, FND-AI-009 |
| `0x45FE` | 4 | `INT32LE` | `scenario` | Block 7, a copy of `0x004ABBE8`. The glossary's `scenario`. | supported | FND-SAVE-001, FND-SETUP-009, FND-AI-002 |
| `0x4602` | 4 | `INT32LE` | `turn_limit` | Block 8, a copy of `0x004A5EF8`. The match's turn limit. | sourced | FND-SAVE-001, SRC-RECHAOS-3561D41 |
| `0x4606` | 24 | `INT32LE[6]` | `cash` | Block 9, a copy of `0x004A25E8`. Per player, cash. | supported | FND-SAVE-001, FND-SETUP-001, FND-UPKEEP-001 |
| `0x461E` | 18 | `INT8[18]` | `hire_offers` | Block 10, a copy of `0x004ABBC0`. Three hire offers per player at `player * 3 + slot`. | supported | FND-SAVE-001, FND-HIRE-001 |
| `0x4630` | 18 | `INT8[18]` | `hire_orders` | Block 11, a copy of `0x004A27C8`. The hire orders that go with `hire_offers`, at the same index. | supported | FND-SAVE-001, FND-HIRE-001, FND-HIRE-004 |
| `0x4642` | 384 | `UINT8[384]` | `research_remaining` | Block 12, a copy of `0x004A2608`. Per item and player, at `item * 6 + player`, the research still needed. | supported | FND-SAVE-001, FND-RESEARCH-001, FND-RESEARCH-002 |
| `0x47C2` | 6 | `UINT8[6]` | `player_active` | Block 13, a copy of `0x004ABBE0`. Per player, whether the player is still in the match; the glossary's `player_active`, whose address no finding has yet recorded. | sourced | FND-SAVE-001, SRC-RECHAOS-3561D41 |
| `0x47C8` | 7776 | `BYTE[7776]` | `ai_plans` | Block 14, a copy of `0x0048A250`. The computer players' planning records, 16 bytes per player and roster slot at `player * 0x510 + slot * 0x10`. | supported | FND-SAVE-001, FND-AI-001, FND-AI-019 |
| `0x6628` | 6 | `UINT8[6]` | `ai_first_plan_flags` | Block 15, a copy of `0x00482108`. Per player, the first-plan flag of the planning records. | supported | FND-SAVE-001, FND-AI-019 |
| `0x662E` | 24 | `INT32LE[6]` | `ai_hire_limits` | Block 16, a copy of `0x00482110`. Per player, the gang count below which the computer player hires; every planning pass recomputes it before use. | supported | FND-SAVE-001, FND-STATE-003, FND-AI-012 |
| `0x6646` | 1944 | `INT32LE[486]` | `ai_unused_gang_values` | Block 17, a copy of `0x0048DB48`. Per player and roster slot, a value only AI selector `0x5D` reads; the only store writes 0, so it is 0 in every match. | supported | FND-SAVE-001, FND-STATE-003, FND-AI-041 |
| `0x6DDE` | 6 | `UINT8[6]` | `ai_takeover_flags` | Block 18, a copy of `0x00482158`. Per player, set when a computer player takes over a network player; every gang of that player is then planned as a raider. | supported | FND-SAVE-001, FND-STATE-003, FND-AI-043 |
| `0x6DE4` | 24 | `INT32LE[6]` | `ai_hire_anchors` | Block 19, a copy of `0x0048E2F8`. Per player, the encoded hire placement anchor. | supported | FND-SAVE-001, FND-AI-010 |
| `0x6DFC` | 24 | `INT32LE[6]` | `ai_hire_roles` | Block 20, a copy of `0x00482128`. Per player, the hire role the planner chose. | supported | FND-SAVE-001, FND-AI-002, FND-AI-009 |
| `0x6E14` | 24 | `INT32LE[6]` | `ai_unused_hire_flags` | Block 21, a copy of `0x00482140`. Per player, 0 or 1, written with each hire-role choice and never read. | supported | FND-SAVE-001, FND-STATE-003, FND-AI-042 |
| `0x6E2C` | 24 | `INT32LE[6]` | `scenario_score` | Block 22, a copy of `0x004A2790`. The glossary's `scenario_score`. | supported | FND-SAVE-001, FND-AI-005 |
| `0x6E44` | 256 | `INT16LE[128]` | `crackdown_history` | Block 23, a copy of `0x004ABCC0`. Per sector, the last two Crackdown turns, at `sector * 4` and `sector * 4 + 2`. | supported | FND-SAVE-001, FND-POLICE-001 |
| `0x6F44` | 1920 | `BYTE[1920]` | `reports` | Block 24, a copy of `0x004AAE08`. The Last Turn reports: 32 records of 10 bytes per player. | supported | FND-SAVE-001, FND-EVENT-001 |
| `0x76C4` | 9600 | `BYTE[9600]` | `combat_results` | Block 25, a copy of `0x004A8888`. The Combat Results table: `0x96` bytes per sector and `0x18` bytes per player. | supported | FND-SAVE-001, FND-AUDIO-011 |
| `0x9C44` | 4860 | `FMT-STATE-003[486]` | `combat_records` | Block 26, a copy of `0x004A11E8`. One combat record per player and roster slot. | supported | FND-SAVE-001, FND-COMBAT-004 |
| `0xAF40` | 2 | `INT16LE` | `current_player` | Block 27, a copy of `0x00494830`. The player whose turn it is. | sourced | FND-SAVE-001, SRC-RECHAOS-3561D41 |
| `0xAF42` | 72 | `BYTE[72]` | `player_names` | Block 28, a copy of `0x004A2588`. The glossary's `player_names`: six 12-byte records, the name's characters starting at each record's second byte. | supported | FND-SAVE-001, FND-UI-003 |
| `0xAF8A` | 24 | `INT32LE[6]` | `overthrow_count` | Block 29, a copy of `0x004A27A8`. The glossary's `overthrow_count`. | supported | FND-SAVE-001, FND-AWARDS-001 |
| `0xAFA2` | 24 | `INT32LE[6]` | `damage_inflicted` | Block 30, a copy of `0x004A5ED8`. Per player, the damage inflicted by the player's own attacks. | supported | FND-SAVE-001, FND-COMBAT-003, FND-AWARDS-001 |
| `0xAFBA` | 24 | `INT32LE[6]` | `hide_count` | Block 31, a copy of `0x004A25D0`. Per player, the number of Hide actions carried out. | supported | FND-SAVE-001, FND-AWARDS-002 |
| `0xAFD2` | 24 | `INT32LE[6]` | `cash_spent` | Block 32, a copy of `0x0049CA78`. Per player, the cash spent. | supported | FND-SAVE-001, FND-UPKEEP-001, FND-AWARDS-001 |
| `0xAFEA` | 24 | `INT32LE[6]` | `casualties` | Block 33, a copy of `0x004AB620`. Per player, the casualties. | sourced | FND-SAVE-001, SRC-RECHAOS-3561D41 |
| `0xB002` | 24 | `INT32LE[6]` | `cash_earned` | Block 34, a copy of `0x004A27E0`. Per player, the cash earned. | supported | FND-SAVE-001, FND-UPKEEP-001 |
| `0xB01A` | 144 | `INT32LE[36]` | `attitudes` | Block 35, a copy of `0x004AB590`. The six-by-six attitude matrix of signed values. | supported | FND-SAVE-001, FND-AI-006 |
| `0xB0AA` | 24 | `INT32LE[6]` | `reactions` | Block 36, a copy of `0x004AB650`. Per player, the reaction value that scales the attitude drops. | supported | FND-SAVE-001, FND-STATE-003, FND-AI-006 |
| `0xB0C2` | 6 | `UINT8[6]` | `homicidal_maniac_flags` | Block 37, a copy of `0x004A2600`. Per player, 1 when the match was set up under the Homicidal Maniac Mentality, else 0; never read. | supported | FND-SAVE-001, FND-STATE-003, FND-SETUP-015 |
| `0xB0C8` | 24 | `INT32LE[6]` | `difficulty_bands` | Block 38, a copy of `0x004A2570`. Per player, the difficulty band set from the Mentality. | supported | FND-SAVE-001, FND-AI-007 |
| `0xB0E0` | 6 | `UINT8[6]` | `players_human` | Block 39, a copy of `0x004ABC58`. Per player, whether the player is human. | sourced | FND-SAVE-001, SRC-RECHAOS-3561D41 |
| `0xB0E6` | 1 | `UINT8` | `preference_1` | Block 40. The first preference byte, from `0x00487850`. Copied to the live preferences only after the closing marker matches. | supported | FND-SAVE-001 |
| `0xB0E7` | 1 | `UINT8` | `preference_2` | Block 41. The second preference byte, from `0x00487854`. | supported | FND-SAVE-001 |
| `0xB0E8` | 1 | `UINT8` | `preference_3` | Block 42. The third preference byte, from `0x00487858`. | supported | FND-SAVE-001 |
| `0xB0E9` | 6 | `UINT8[6]` | `modifier_visibility` | Block 43, a copy of `0x004AB588`. Per player, the flag that the name `modifier_name_visibility` sets. | supported | FND-SAVE-001, FND-SETUP-011 |
| `0xB0EF` | 6 | `UINT8[6]` | `modifier_force_hire` | Block 44, a copy of `0x004A5EF0`. Per player, the flag that makes every hire start at Force 10. | supported | FND-SAVE-001, FND-HIRE-005 |
| `0xB0F5` | `24 if magic == SAVE_MAGIC_N40W` | `INT32LE[6]` | `participants` | Only in the network form: the six participant mappings from `0x00498968`. | supported | FND-SAVE-001 |
| | 4 | `UINT32LE` | `closing_magic` | A second copy of `magic`. A different value makes the load beep and return 0. | supported | FND-SAVE-001 |
| | | | | Total size 45,305 (`0xB0F9`) for `SAVE_MAGIC_S40W`, 45,329 (`0xB111`) for `SAVE_MAGIC_N40W` | | |

## Enumerations and flags

### `magic`

| Value | Name | Meaning | Status | Evidence |
|---|---|---|---|---|
| `0x57303453` | `SAVE_MAGIC_S40W` | `S40W` on disk: the full save. The load returns 1. | supported | FND-SAVE-001 |
| `0x5730344E` | `SAVE_MAGIC_N40W` | `N40W` on disk: the full save with `participants`. The load returns 2. | supported | FND-SAVE-001 |

## Differences between builds

The source's table, made from another executable, has the same blocks at the
same offsets and the same two sizes.

## Coverage

No save file was examined. The block list, the sizes and the globals were read
from the load and save functions of BLD-GOG-EN-1.1 (FND-SAVE-001), and the
offsets are their running sums; they match the offsets in the source's table
row for row. The Kaitai definition has not been compiled or run against a
save.

## Open questions

- The meanings of blocks 3, 4, 8, 13, 27, 33 and 39 rest on the source alone.
- Which of the three preferences each preference byte is (the source names
  them difficulty, time limit and objective) has not been traced here; the
  options findings own the preference globals.
- The loader checks neither the byte count of any read nor the file size, so a
  short file overwrites part of the state and leaves the rest (FND-SAVE-001).
