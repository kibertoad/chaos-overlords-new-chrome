---
id: RULE-OBJECTIVE-004
title: Each scenario's own end condition, and the Dominance weights
status: sourced
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-TURN-003, FND-UI-033, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-OBJECTIVE-002]
---

## Summary

The timed scenarios (Greed, Power, Acceptance, Dominance) end when the chosen
time limit runs out. Big 40 ends when a player holds 40 sectors, Big Man when a
player has 40 points, Siege when one player holds all six headquarters
sectors, and Armageddon when a player holds all 64 sectors. Kill 'Em All and
Eliminate end only when one player is left.

## When it runs

Called by RULE-OBJECTIVE-001 at the end of every turn, after the scores are
rebuilt and when more than one player is still active.

## Parameters

None.

## Inputs

`scenario`, `scenario_score`, `player_active`, `turn_limit`, `elapsed_turns`,
each sector's `owner`, `hq_sectors`, `cash`.

## Procedure

```text
define dominance_points(player) -> INT32:
    let support_weight = 10
    let sector_weight = 30
    if turn_limit == 52:
        support_weight = 30
        sector_weight = 100
    else if turn_limit == 104:
        support_weight = 75
        sector_weight = 250
    else if turn_limit == 208:
        support_weight = 300
        sector_weight = 1000
    let support = 0
    let held = 0
    for s in 0..64:
        if sectors[s].owner == player:
            support = support + sectors[s].support
            held = held + 1
    return cash[player] + support * support_weight + held * sector_weight

if scenario == scenario_greed or scenario == scenario_power or scenario == scenario_acceptance or scenario == scenario_dominance:
    return elapsed_turns + 1 >= turn_limit
for each player in turn_order:
    if player_active[player]:
        if scenario == 8 and scenario_score[player] >= 40:
            # Big Man
            return true
        if scenario == scenario_big_40 and scenario_score[player] >= 40:
            return true
        if scenario == 9 and scenario_score[player] >= 64:
            # Armageddon
            return true
        if scenario == 6:
            # Siege
            let held = 0
            for k in 0..6:
                if sectors[hq_sectors[k]].owner == player:
                    held = held + 1
            if held == 6:
                return true
return false
```

## Outputs

Returns `true` when the scenario's own condition is met, as an `INT32`. Changes
no state and makes no draws.

## Edge cases

Two players can reach a threshold in the same turn; the manual says the winner
of a timed scenario is the player with the highest score, and gives no rule
for a tie.

## What the sources say

SRC-MANUAL-GOG, page 12, gives the time limits of the timed scenarios as 6
months (26 turns) to 4 years (208 turns); page 13 gives the Dominance weights
used in `dominance_points` for 6 months, 1 year, 2 years and 4 years; page 14
gives the objectives of Kill 'Em All, Big 40, Eliminate, Siege, Big Man (40
points) and Armageddon (all 64 sectors). FND-TURN-003 confirms from the
executable that Big Man ends at a score of 40 or more. FND-UI-033 confirms
that the Siege landmarks are the six headquarters sectors.

## Differences between builds

None known.

## Open questions

- Only the Big Man threshold is recorded from the executable. The Big 40,
  Siege and Armageddon conditions, the time-limit test and its exact turn
  count, and the Dominance weights come from the manual.
- The values of `scenario` for the timed scenarios and Big 40 are not
  recorded (`scenario_greed`, `scenario_power`, `scenario_acceptance`,
  `scenario_dominance`, `scenario_big_40`).
- Where the game keeps `turn_limit`, and whether `elapsed_turns` counts the
  turn just resolved at this point, are not recorded.
- Whether the executable's Dominance numerator uses the manual's weights is
  not recorded.
