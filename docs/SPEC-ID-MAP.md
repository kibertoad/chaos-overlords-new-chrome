# Spec ID map

Before the documentation followed the
[documentation standard](https://dinorefurb.com/documentation-standard/), static findings were
headed `BIN-*` in `docs/original-internals/` and rules `RULE-*` in `docs/GAME-RULES.md`. Those
documents are gone. This table gives the spec entry that holds each old finding now, so an old
commit message, pull request or review that cites one can still be followed. Where an old finding
was split, every entry it became is listed.

Every old `RULE-*` ID kept its ID in `spec/rules/` except the ones in the second table. Old
format sections in `docs/ORIGINAL-FILE-FORMATS.md`, the screen notes in `docs/UI-ATLAS.md` and
the rows of `docs/PARITY-MATRIX.md` had no IDs; their content is in the `FMT-*` and `SCR-*`
entries and in [PARITY.md](../PARITY.md).

This file is not maintained further. New IDs never need a row here.

## Findings

| Old ID | Spec entries |
|---|---|
| `BIN-AI-001` | [`FND-AI-001`](../spec/findings/FND-AI-001.md) |
| `BIN-AI-002` | [`FND-AI-002`](../spec/findings/FND-AI-002.md) |
| `BIN-AI-003` | [`FND-AI-003`](../spec/findings/FND-AI-003.md) |
| `BIN-AI-003A` | [`FND-AI-008`](../spec/findings/FND-AI-008.md), [`FND-AI-011`](../spec/findings/FND-AI-011.md) |
| `BIN-AI-003B` | [`FND-AI-009`](../spec/findings/FND-AI-009.md), [`FND-AI-012`](../spec/findings/FND-AI-012.md), [`FND-AI-013`](../spec/findings/FND-AI-013.md), [`FND-AI-014`](../spec/findings/FND-AI-014.md), [`FND-AI-015`](../spec/findings/FND-AI-015.md) |
| `BIN-AI-003C` | [`FND-AI-010`](../spec/findings/FND-AI-010.md), [`FND-AI-016`](../spec/findings/FND-AI-016.md), [`FND-AI-017`](../spec/findings/FND-AI-017.md) |
| `BIN-AI-004` | [`FND-AI-004`](../spec/findings/FND-AI-004.md), [`FND-AI-018`](../spec/findings/FND-AI-018.md), [`FND-AI-019`](../spec/findings/FND-AI-019.md), [`FND-AI-020`](../spec/findings/FND-AI-020.md), [`FND-AI-021`](../spec/findings/FND-AI-021.md), [`FND-AI-022`](../spec/findings/FND-AI-022.md), [`FND-AI-023`](../spec/findings/FND-AI-023.md) |
| `BIN-AI-005` | [`FND-AI-005`](../spec/findings/FND-AI-005.md) |
| `BIN-AI-006` | [`FND-AI-006`](../spec/findings/FND-AI-006.md), [`FND-AI-024`](../spec/findings/FND-AI-024.md) |
| `BIN-AI-007` | [`FND-AI-007`](../spec/findings/FND-AI-007.md) |
| `BIN-API-001` | [`FND-PLATFORM-001`](../spec/findings/FND-PLATFORM-001.md) |
| `BIN-API-002` | [`FND-PLATFORM-002`](../spec/findings/FND-PLATFORM-002.md), [`FND-PLATFORM-007`](../spec/findings/FND-PLATFORM-007.md), [`FND-PLATFORM-008`](../spec/findings/FND-PLATFORM-008.md) |
| `BIN-API-003` | [`FND-PLATFORM-003`](../spec/findings/FND-PLATFORM-003.md), [`FND-SAVE-001`](../spec/findings/FND-SAVE-001.md) |
| `BIN-API-004` | [`FND-PLATFORM-004`](../spec/findings/FND-PLATFORM-004.md) |
| `BIN-API-005` | [`FND-PLATFORM-005`](../spec/findings/FND-PLATFORM-005.md) |
| `BIN-API-006` | [`FND-PLATFORM-006`](../spec/findings/FND-PLATFORM-006.md) |
| `BIN-ASSET-001` | [`FND-ASSET-001`](../spec/findings/FND-ASSET-001.md) |
| `BIN-ASSET-002` | [`FND-HELP-001`](../spec/findings/FND-HELP-001.md) |
| `BIN-ASSET-003` | [`FND-HELP-002`](../spec/findings/FND-HELP-002.md) |
| `BIN-ATTACK-001` | [`FND-ATTACK-001`](../spec/findings/FND-ATTACK-001.md) |
| `BIN-AWARDS-001` | [`FND-AWARDS-001`](../spec/findings/FND-AWARDS-001.md), [`FND-AI-025`](../spec/findings/FND-AI-025.md), [`FND-AI-026`](../spec/findings/FND-AI-026.md), [`FND-AI-027`](../spec/findings/FND-AI-027.md), [`FND-AI-028`](../spec/findings/FND-AI-028.md), [`FND-AI-029`](../spec/findings/FND-AI-029.md), [`FND-AI-030`](../spec/findings/FND-AI-030.md), [`FND-AI-031`](../spec/findings/FND-AI-031.md), [`FND-AI-032`](../spec/findings/FND-AI-032.md), [`FND-AI-033`](../spec/findings/FND-AI-033.md), [`FND-AI-034`](../spec/findings/FND-AI-034.md), [`FND-AI-035`](../spec/findings/FND-AI-035.md), [`FND-AI-036`](../spec/findings/FND-AI-036.md), [`FND-AI-037`](../spec/findings/FND-AI-037.md), [`FND-AI-038`](../spec/findings/FND-AI-038.md), [`FND-AI-039`](../spec/findings/FND-AI-039.md), [`FND-AI-040`](../spec/findings/FND-AI-040.md), [`FND-AWARDS-002`](../spec/findings/FND-AWARDS-002.md) |
| `BIN-BRIBE-001` | [`FND-BRIBE-001`](../spec/findings/FND-BRIBE-001.md) |
| `BIN-CHAOS-001` | [`FND-CHAOS-001`](../spec/findings/FND-CHAOS-001.md) |
| `BIN-CITY-001` | [`FND-CITY-001`](../spec/findings/FND-CITY-001.md) |
| `BIN-CITY-002` | [`FND-CITY-002`](../spec/findings/FND-CITY-002.md) |
| `BIN-CITY-003` | [`FND-CITY-003`](../spec/findings/FND-CITY-003.md) |
| `BIN-COMBAT-ORDER-001` | [`FND-COMBAT-001`](../spec/findings/FND-COMBAT-001.md) |
| `BIN-COMBAT-PRESENT-001` | [`FND-COMBAT-004`](../spec/findings/FND-COMBAT-004.md), [`FND-COMBAT-005`](../spec/findings/FND-COMBAT-005.md), [`FND-COMBAT-006`](../spec/findings/FND-COMBAT-006.md) |
| `BIN-COMBAT-RESULTS-001` | [`FND-COMBAT-002`](../spec/findings/FND-COMBAT-002.md) |
| `BIN-COMBAT-STATS-001` | [`FND-COMBAT-003`](../spec/findings/FND-COMBAT-003.md) |
| `BIN-COMLINK-001` | [`FND-COMLINK-001`](../spec/findings/FND-COMLINK-001.md) |
| `BIN-COMLINK-002` | [`FND-COMLINK-002`](../spec/findings/FND-COMLINK-002.md) |
| `BIN-COMLINK-003` | [`FND-COMLINK-003`](../spec/findings/FND-COMLINK-003.md), [`FND-COMLINK-005`](../spec/findings/FND-COMLINK-005.md) |
| `BIN-COMLINK-004` | [`FND-COMLINK-004`](../spec/findings/FND-COMLINK-004.md) |
| `BIN-COMMAND-ASSIGN-001` | [`FND-TURN-002`](../spec/findings/FND-TURN-002.md) |
| `BIN-CONTROL-001` | [`FND-CONTROL-001`](../spec/findings/FND-CONTROL-001.md), [`FND-CONTROL-002`](../spec/findings/FND-CONTROL-002.md) |
| `BIN-DETECT-001` | [`FND-DETECT-001`](../spec/findings/FND-DETECT-001.md) |
| `BIN-EFFECTIVE-STATS-001` | [`FND-GANG-001`](../spec/findings/FND-GANG-001.md) |
| `BIN-ENDTURN-001` | [`FND-TURN-003`](../spec/findings/FND-TURN-003.md) |
| `BIN-EQUIP-001` | [`FND-EQUIP-001`](../spec/findings/FND-EQUIP-001.md) |
| `BIN-EQUIP-002` | [`FND-EQUIP-002`](../spec/findings/FND-EQUIP-002.md) |
| `BIN-EQUIP-003` | [`FND-EQUIP-003`](../spec/findings/FND-EQUIP-003.md) |
| `BIN-EQUIP-004` | [`FND-EQUIP-004`](../spec/findings/FND-EQUIP-004.md) |
| `BIN-EQUIP-005` | [`FND-EQUIP-005`](../spec/findings/FND-EQUIP-005.md) |
| `BIN-EQUIP-006` | [`FND-EQUIP-006`](../spec/findings/FND-EQUIP-006.md) |
| `BIN-EVENT-001` | [`FND-EVENT-001`](../spec/findings/FND-EVENT-001.md) |
| `BIN-EVENT-003` | [`FND-EVENT-003`](../spec/findings/FND-EVENT-003.md) |
| `BIN-EVENTS-002` | [`FND-EVENT-002`](../spec/findings/FND-EVENT-002.md) |
| `BIN-FINANCE-001` | [`FND-FINANCE-001`](../spec/findings/FND-FINANCE-001.md) |
| `BIN-GAME-INFO-001` | [`FND-UI-003`](../spec/findings/FND-UI-003.md) |
| `BIN-GANG-DEFINITION-001` | [`FND-GANG-002`](../spec/findings/FND-GANG-002.md) |
| `BIN-GANG-RETIRE-001` | [`FND-GANG-003`](../spec/findings/FND-GANG-003.md) |
| `BIN-GANG-VALUES-001` | [`FND-GANG-004`](../spec/findings/FND-GANG-004.md) |
| `BIN-HIDE-LIFECYCLE-001` | [`FND-HIDE-001`](../spec/findings/FND-HIDE-001.md) |
| `BIN-HIRE-001` | [`FND-HIRE-001`](../spec/findings/FND-HIRE-001.md), [`FND-HIRE-004`](../spec/findings/FND-HIRE-004.md), [`FND-HIRE-005`](../spec/findings/FND-HIRE-005.md) |
| `BIN-HIRE-002` | [`FND-HIRE-002`](../spec/findings/FND-HIRE-002.md) |
| `BIN-HIRE-COMPARISON-001` | [`FND-HIRE-003`](../spec/findings/FND-HIRE-003.md) |
| `BIN-HOTSEAT-002` | [`FND-SETUP-010`](../spec/findings/FND-SETUP-010.md), [`FND-OBJECTIVE-002`](../spec/findings/FND-OBJECTIVE-002.md), [`FND-AWARDS-003`](../spec/findings/FND-AWARDS-003.md) |
| `BIN-INFLUENCE-001` | [`FND-INFLUENCE-001`](../spec/findings/FND-INFLUENCE-001.md) |
| `BIN-INSTANT-001` | [`FND-TURN-001`](../spec/findings/FND-TURN-001.md) |
| `BIN-ITEM-INFO-001` | [`FND-UI-004`](../spec/findings/FND-UI-004.md) |
| `BIN-MOVEMENT-001` | [`FND-MOVE-001`](../spec/findings/FND-MOVE-001.md) |
| `BIN-MOVEMENT-002` | [`FND-MOVE-002`](../spec/findings/FND-MOVE-002.md) |
| `BIN-MUSIC-001` | [`FND-AUDIO-001`](../spec/findings/FND-AUDIO-001.md) |
| `BIN-NUMBER-HELPERS-001` | [`FND-UI-006`](../spec/findings/FND-UI-006.md) |
| `BIN-OPTIONS-001` | [`FND-OPTIONS-001`](../spec/findings/FND-OPTIONS-001.md), [`FND-UI-011`](../spec/findings/FND-UI-011.md), [`FND-OPTIONS-002`](../spec/findings/FND-OPTIONS-002.md), [`FND-TIMER-001`](../spec/findings/FND-TIMER-001.md) |
| `BIN-PE-001` | [`FND-EXE-001`](../spec/findings/FND-EXE-001.md) |
| `BIN-PE-002` | [`FND-EXE-002`](../spec/findings/FND-EXE-002.md) |
| `BIN-POLICE-001` | [`FND-POLICE-001`](../spec/findings/FND-POLICE-001.md) |
| `BIN-POLICE-002` | [`FND-POLICE-002`](../spec/findings/FND-POLICE-002.md) |
| `BIN-POLICE-COMBAT-001` | [`FND-POLICE-003`](../spec/findings/FND-POLICE-003.md) |
| `BIN-RANKING-001` | [`FND-OBJECTIVE-001`](../spec/findings/FND-OBJECTIVE-001.md) |
| `BIN-REPEAT-001` | [`FND-TURN-004`](../spec/findings/FND-TURN-004.md) |
| `BIN-RESEARCH-000` | [`FND-RESEARCH-002`](../spec/findings/FND-RESEARCH-002.md) |
| `BIN-RESEARCH-001` | [`FND-RESEARCH-001`](../spec/findings/FND-RESEARCH-001.md) |
| `BIN-RNG-001` | [`FND-RNG-001`](../spec/findings/FND-RNG-001.md) |
| `BIN-RNG-002` | [`FND-RNG-002`](../spec/findings/FND-RNG-002.md) |
| `BIN-RNG-003` | [`FND-RNG-003`](../spec/findings/FND-RNG-003.md) |
| `BIN-RNG-004` | [`FND-RNG-004`](../spec/findings/FND-RNG-004.md) |
| `BIN-RNG-005` | [`FND-RNG-005`](../spec/findings/FND-RNG-005.md) |
| `BIN-SEARCH-001` | [`FND-SEARCH-001`](../spec/findings/FND-SEARCH-001.md), [`FND-SEARCH-003`](../spec/findings/FND-SEARCH-003.md) |
| `BIN-SEARCH-002` | [`FND-SEARCH-002`](../spec/findings/FND-SEARCH-002.md) |
| `BIN-SECTOR-GANGS-001` | [`FND-UI-002`](../spec/findings/FND-UI-002.md) |
| `BIN-SETUP-000` | [`FND-SETUP-009`](../spec/findings/FND-SETUP-009.md), [`FND-SETUP-012`](../spec/findings/FND-SETUP-012.md) |
| `BIN-SETUP-001` | [`FND-SETUP-001`](../spec/findings/FND-SETUP-001.md) |
| `BIN-SETUP-002` | [`FND-SETUP-002`](../spec/findings/FND-SETUP-002.md) |
| `BIN-SETUP-003` | [`FND-SETUP-003`](../spec/findings/FND-SETUP-003.md) |
| `BIN-SETUP-004` | [`FND-SETUP-004`](../spec/findings/FND-SETUP-004.md), [`FND-SETUP-011`](../spec/findings/FND-SETUP-011.md) |
| `BIN-SETUP-005` | [`FND-SETUP-005`](../spec/findings/FND-SETUP-005.md) |
| `BIN-SETUP-006` | [`FND-SETUP-006`](../spec/findings/FND-SETUP-006.md) |
| `BIN-SETUP-007` | [`FND-SETUP-007`](../spec/findings/FND-SETUP-007.md) |
| `BIN-SETUP-008` | [`FND-SETUP-008`](../spec/findings/FND-SETUP-008.md) |
| `BIN-SITE-INFO-001` | [`FND-UI-005`](../spec/findings/FND-UI-005.md) |
| `BIN-SNITCH-001` | [`FND-SNITCH-001`](../spec/findings/FND-SNITCH-001.md) |
| `BIN-SOUND-001` | [`FND-AUDIO-002`](../spec/findings/FND-AUDIO-002.md), [`FND-AUDIO-010`](../spec/findings/FND-AUDIO-010.md), [`FND-AUDIO-011`](../spec/findings/FND-AUDIO-011.md), [`FND-AUDIO-012`](../spec/findings/FND-AUDIO-012.md), [`FND-AUDIO-013`](../spec/findings/FND-AUDIO-013.md), [`FND-UI-012`](../spec/findings/FND-UI-012.md) |
| `BIN-SOUND-002` | [`FND-AUDIO-003`](../spec/findings/FND-AUDIO-003.md) |
| `BIN-TOOL-001` | [`FND-EXE-003`](../spec/findings/FND-EXE-003.md) |
| `BIN-TURN-PLAYER-ORDER-001` | [`FND-TURN-005`](../spec/findings/FND-TURN-005.md) |
| `BIN-UI-001` | [`FND-UI-001`](../spec/findings/FND-UI-001.md), [`FND-UI-010`](../spec/findings/FND-UI-010.md) |
| `BIN-UI-016` | [`FND-UI-016`](../spec/findings/FND-UI-016.md) |
| `BIN-UI-031` | [`FND-UI-031`](../spec/findings/FND-UI-031.md) |
| `BIN-UI-032` | [`FND-UI-032`](../spec/findings/FND-UI-032.md) |
| `BIN-UI-033` | [`FND-UI-033`](../spec/findings/FND-UI-033.md) |
| `BIN-UI-034` | [`FND-UI-034`](../spec/findings/FND-UI-034.md) |
| `BIN-UI-035` | [`FND-UI-035`](../spec/findings/FND-UI-035.md) |
| `BIN-UI-036` | [`FND-UI-036`](../spec/findings/FND-UI-036.md) |
| `BIN-UI-CREDITS-001` | [`FND-UI-007`](../spec/findings/FND-UI-007.md) |
| `BIN-UI-MENU-001` | [`FND-UI-008`](../spec/findings/FND-UI-008.md) |
| `BIN-UI-TITLE-001` | [`FND-UI-009`](../spec/findings/FND-UI-009.md) |
| `BIN-UPKEEP-001` | [`FND-UPKEEP-001`](../spec/findings/FND-UPKEEP-001.md) |

## Renamed rules

| Old ID | Spec entries |
|---|---|
| `RULE-SITE-STATS-001` | [`RULE-SITE-001`](../spec/rules/RULE-SITE-001.md) |
