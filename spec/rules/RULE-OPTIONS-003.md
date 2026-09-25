---
id: RULE-OPTIONS-003
title: Warn if Idle Gangs asks before Done ends a turn with a gang left idle
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-OPTIONS-002, FND-OPTIONS-001, FND-OPTIONS-003, FND-STATE-010, FND-EXE-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [FMT-STATE-001, SCR-OPTIONS-001]
---

## Summary

With Warn if Idle Gangs on, pressing Done while one of the player's gangs has
no order opens a warning. OK ends the turn anyway and Cancel goes back to
planning. Idle gangs stay idle either way.

## When it runs

When a human player's press of Done on the main console is accepted
(RULE-UI-002), before the planning turn ends.

## Parameters

None.

## Inputs

`pref_warn_idle`, `no_match_in_play`, `active_player`, `gangs`, `idle_warning_choice`.

## Procedure

```text
if pref_warn_idle == 0 or no_match_in_play != 0:
    return 1
let idle = 0
for slot in 0..81:
    let gang = gangs[active_player * 81 + slot]
    if gang.sector != GANG_INACTIVE and gang.action == ACTION_NONE:
        idle = 1
if idle == 0:
    return 1
show SCR-OPTIONS-001
return idle_warning_choice
```

## Outputs

Returns 1 when the turn may end and 0 when the player chose Cancel. Shows
SCR-OPTIONS-001 when a gang is idle.

## Edge cases

- A gang with a recurring order has that order in `action` at the start of the
  turn and does not count as idle.
- The warning is not shown when a turn ends because its time limit passed
  (RULE-TIMER-002).
- The scan reads only the active player's 81 slots and does not stop at the
  first idle gang.
- The warning is also skipped while `no_match_in_play` is set, which is the
  case in the last planning passes after a match has ended (FND-STATE-010).

## What the sources say

SRC-MANUAL-GOG, page 10, says that with Warn If Idle Gangs checked the game
warns a player who presses Done without giving every gang a command, and that
it is checked by default; page 26 says the same under Done. They agree with the
executable.

## Differences between builds

None known.

## Open questions

None.
