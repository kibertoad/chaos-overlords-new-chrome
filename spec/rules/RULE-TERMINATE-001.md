---
id: RULE-TERMINATE-001
title: Terminate pass retires every gang ordered to Terminate, before any Move
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-MOVE-001, FND-MOVE-003, FND-GANG-003, FND-CONTROL-002, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-GANG-002, FMT-STATE-001]
---

## Summary

Every gang ordered to Terminate leaves play. All Terminates are carried out,
player by player and roster slot by roster slot, before any gang moves.

## When it runs

`terminate_phase`: after `chaos_payout_phase` and before `move_phase`.

## Parameters

None.

## Inputs

`turn_order`, `gangs`, and each gang's `action`.

## Procedure

```text
for each player in turn_order:
    for slot in 0..81:
        let gang = gangs[player * 81 + slot]
        if gang.sector != GANG_INACTIVE and gang.action == ACTION_TERMINATE:
            call RULE-GANG-002(player, gang, false)
```

## Outputs

No return value. Sets `sector` to 100 (`GANG_INACTIVE`) for each gang ordered
to Terminate, through RULE-GANG-002, and changes nothing else in its record.
Makes no random draw.

## Edge cases

- The terminated gang's items stay in its inactive record and are lost to the
  player (RULE-GANG-002).
- A terminated gang frees its place in the sector before RULE-MOVE-002 counts
  the player's gangs, so a Move into that sector can use it.
- Terminating the player's last gang in a sector does not give up the sector
  (FND-CONTROL-002).
- A gang that died in this turn's combat is skipped; so is a Terminate order
  left on any other inactive record [FND-MOVE-003].
- A Terminate records no Last Turn report and changes no statistic; in
  particular it does not count as a casualty [FND-MOVE-003, FND-GANG-005].

## What the sources say

SRC-MANUAL-GOG, page 34 (Terminate), says Terminate removes the gang from play
together with all its items. Page 45 (Command Sequence) puts Terminate with
Move in the Movement phase. The executable leaves the items in the retired
record, which the player can no longer reach, so the manual's description holds
for play.

## Differences between builds

None known.

## Open questions

None known.
