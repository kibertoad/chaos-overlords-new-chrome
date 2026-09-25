---
id: RULE-OBJECTIVE-004
title: Each scenario's own end condition, and the Dominance weights
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-OBJECTIVE-003, FND-OBJECTIVE-006, FND-TURN-003, FND-UI-033, SRC-MANUAL-GOG, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-OBJECTIVE-002]
---

## Summary

The timed scenarios (Greed, Power, Acceptance, Dominance) end with the
resolution of the turn numbered `turn_limit`. Big 40 ends when a player holds
40 sectors, Big Man when a player has 40 points, Siege when one player holds
all six headquarters sectors, and Armageddon when a player holds all 64
sectors. Kill 'Em All and Eliminate have no test of their own and end only
when one player is left (RULE-OBJECTIVE-001).

## When it runs

Called by RULE-OBJECTIVE-001 at the end of every turn, after the scores are
rebuilt, whatever the number of active players.

## Parameters

None.

## Inputs

`scenario`, `scenario_score`, `turn_limit`, `elapsed_turns`, each sector's
`owner`, `hq_sectors`; for `dominance_points`, `cash`, each sector's `sites`
and the site definitions.

## Procedure

```text
# Used by RULE-OBJECTIVE-002 for Dominance. The weights are set only for the
# four time limits; the executable leaves them undefined for any other value.
define dominance_points(player) -> INT32:
    let cash_weight = 1
    let sector_weight = 0
    let support_weight = 0
    if turn_limit == 26:
        sector_weight = 30
        support_weight = 10
    else if turn_limit == 52:
        sector_weight = 100
        support_weight = 30
    else if turn_limit == 104:
        sector_weight = 250
        support_weight = 75
    else if turn_limit == 208:
        sector_weight = 1000
        support_weight = 300
    let points = cash[player] * cash_weight
    for s in 0..64:
        if sectors[s].owner == player:
            points = points + sector_weight
            for k in 0..3:
                let site = sectors[s].sites[k]
                let d = site_definitions[site.definition]
                if d.resistance == site.progress:
                    points = points + d.support * support_weight
    return points

if scenario == scenario_greed or scenario == scenario_power or scenario == scenario_acceptance or scenario == scenario_dominance:
    return elapsed_turns == turn_limit - 1
if scenario == scenario_big_40:
    # every slot, active or not
    for player in 0..6:
        let held = 0
        for s in 0..64:
            if sectors[s].owner == player:
                held = held + 1
        if held >= 40:
            return true
    return false
if scenario == 6:
    # Siege
    for player in 0..6:
        let held = 0
        for k in 0..6:
            if sectors[hq_sectors[k]].owner == player:
                held = held + 1
        if held == 6:
            return true
    return false
if scenario == 8:
    # Big Man
    for player in 0..6:
        if scenario_score[player] >= 40:
            return true
    return false
if scenario == 9:
    # Armageddon
    for player in 0..6:
        if scenario_score[player] == 64:
            return true
    return false
# Kill 'Em All (4) and Eliminate (7)
return false
```

## Outputs

Returns `true` when the scenario's own condition is met. Changes no state and
makes no draws.

## Edge cases

`elapsed_turns` still holds the number of turns resolved before this one, so
a timed match ends with the resolution of turn `turn_limit`, counted from 1.
The test is an equality: a counter already past `turn_limit - 1` never ends a
timed match. Two players can reach a threshold in the same turn; the rule
only reports that the match is over.

## What the sources say

SRC-MANUAL-GOG, page 12, gives the time limits of the timed scenarios as 6
months (26 turns) to 4 years (208 turns); page 13 gives the Dominance weights
for 6 months, 1 year, 2 years and 4 years, which are the executable's; page
14 gives the objectives of Kill 'Em All, Big 40, Eliminate, Siege, Big Man (40
points) and Armageddon (all 64 sectors). FND-OBJECTIVE-003 reads every test
and weight from the executable.

## Differences between builds

None known.

## Open questions

- None. A loaded or network-restored match carries `scenario`, `turn_limit`
  and `elapsed_turns` unchanged (FND-OBJECTIVE-006), so it can pass the
  timed test only where the match that was saved could.
