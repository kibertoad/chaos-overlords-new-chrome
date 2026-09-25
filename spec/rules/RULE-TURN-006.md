---
id: RULE-TURN-006
title: The end of a turn removes eliminated players, reports each elimination to every player, then evaluates the objective
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-TURN-003, FND-POLICE-001, FND-EVENT-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-POLICE-003, RULE-EVENT-003, RULE-OBJECTIVE-001, FMT-STATE-001, FMT-STATE-002, FMT-STATE-004]
---

## Summary

At the end of each turn, a player who owns no sector and has no gang left is
out of the game. In the Eliminate scenario, a player who has lost the Right
Hands first loses every sector and every gang, so is out as well. Every player
is told of each elimination. Only then does the game check whether the match
is over.

## When it runs

At the end of `resolution`, as `turn_end` (RULE-TURN-002).

## Parameters

None.

## Inputs

`scenario`, `turn_order`, `player_active`, `gangs` (each gang's `sector`),
`sectors` (each sector's `owner` and `sites`).

## Procedure

```text
# where the countdown falls against the steps below is not known
call RULE-POLICE-003()
let was_active = copy(player_active)

if scenario == 7:
    for each player in turn_order:
        if gangs[player * 81].sector == GANG_INACTIVE:
            for each s in sectors:
                if s.owner == player:
                    s.owner = SECTOR_NEUTRAL
                    for i in 0..3:
                        s.sites[i].progress = 0
            for slot in 0..81:
                gangs[player * 81 + slot].sector = GANG_INACTIVE

for each player in turn_order:
    let keeps = false
    for each s in sectors:
        if s.owner == player:
            keeps = true
    for slot in 0..81:
        if gangs[player * 81 + slot].sector != GANG_INACTIVE:
            keeps = true
    if not keeps:
        player_active[player] = 0

call RULE-EVENT-003(was_active)
call RULE-OBJECTIVE-001()
```

## Outputs

No return value. First runs RULE-POLICE-003, the countdown of police presence.
In Eliminate, sets `owner` to -1 and `progress` to 0 in the
sectors of each player without Right Hands, and `sector` to 100 in all 81 of
that player's gang records. Clears `player_active` for each player left with no
sector and no gang. RULE-EVENT-003 then records one type-9 Last Turn report for
each newly eliminated player and each of the six recipients, eliminated players
in slot order and recipients in slot order. Then runs RULE-OBJECTIVE-001. Makes no
draws of its own.

## Edge cases

A player eliminated in an earlier turn is found again by the test but gets no
new report, since the reports compare the active bytes before and after.

The Eliminate retirement writes only each record's `sector`. Force, definition,
equipment and orders keep their values in the inactive records; the equipment
is not returned to anyone, and a later hire into the slot overwrites it.

Eliminated players' reports go to all six slots, including empty and eliminated
ones.

## What the sources say

SRC-MANUAL-GOG, numbered page 44 (Player Elimination), says a player without at
least one sector or gang is eliminated, and that some scenarios eliminate
players in other ways. The executable agrees, and in Eliminate the other way is
the loss of the Right Hands, which takes every sector and gang with it.

## Differences between builds

None known.

## Open questions

- The test the Eliminate scan uses for "slot 0 no longer holds the active Right
  Hands" has not been recorded; the procedure tests the record's `sector`
  only.
- Whether the Eliminate scan skips players who are already inactive has not
  been recorded; it makes no difference to the result.
- Where the countdown of police presence falls against the elimination steps is
  not known.
- The address of `player_active` is not known.
