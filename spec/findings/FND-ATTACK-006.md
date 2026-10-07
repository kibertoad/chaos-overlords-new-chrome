---
id: FND-ATTACK-006
title: The Attack picker's roster builder lists an opponent's gangs in a sector that the active player sees, with no bound on the count
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043D132..0x0043D93C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043B9D8..0x0043B9EA
tool: capstone 5.0.7 disassembly of the hash-verified executable
environment: null
---

## Observation

Offsets are those of the 32-byte gang record (FMT-STATE-001), whose array
starts at `0x00498DA8` with 81 records per player.

- The Attack picker `fn_0043B290` calls `fn_0043D132` at `0x0043B9E5` with two
  stack arguments: the chosen opponent's player slot, read from the picker's
  opponent list, and the acting gang's sector byte, pushed first and so the
  second argument (`0x0043B9D8..0x0043B9E5`). The caller removes the
  arguments.
- `fn_0043D132` sets the six INT32 entries at `0x00494850` to -1
  (`0x0043D13E..0x0043D171`) and draws the picker's target area.
- For roster slots 0 to 80 of the opponent (`0x0043D252..0x0043D26B`) it skips
  the record unless its signed sector byte (offset `0x02`, `0x00498DAA`)
  equals the sector argument (`0x0043D26B..0x0043D28E`) and its byte at offset
  `0x0C` plus `active_player`, the value at `0x004ABC84`, is nonzero
  (`0x0043D28E..0x0043D2B7`).
- For a listed record it draws the target card at a place derived from the
  count's quotient and remainder by 3 (`0x0043D85F..0x0043D888`), stores the
  roster slot in entry `count` and increments the count
  (`0x0043D91D..0x0043D92D`). Nothing compares the count with 6.
- It returns with no value (`0x0043D93B`).

## Interpretation

The targets the picker offers are the opponent's records in the acting gang's
sector that the active player sees, in roster order, as their `visible_to`
entries last stood (RULE-DETECT-001). An inactive record, sector 100, matches
no sector the picker is opened for. The observer is the active player, who is
the acting gang's player whenever the picker is open on that player's own
gang.

## Alternatives

A seventh listed record would be stored past the six entries, at
`0x00494868`. A player keeps at most six gangs in a sector through
hires (RULE-HIRE-001), but some moves are not checked against that limit
(RULE-MOVE-002, RULE-AI-007); what lies after the entries and what a seventh
target would do was not read.

## How to reproduce

Disassemble `0x0043B9D8..0x0043B9EA` and `fn_0043D132` in the hash-verified
executable, and follow the stores to `0x00494850`.
