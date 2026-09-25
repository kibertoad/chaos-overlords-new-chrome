---
id: RULE-OBJECTIVE-002
title: Each player's scenario score is rebuilt from what the scenario counts, and a player's standing is the number of players with a higher score
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-OBJECTIVE-003, FND-AI-005, FND-TURN-003, FND-UI-033, FND-CITY-003, FND-SETUP-009, FND-SETUP-012, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-OBJECTIVE-004, FMT-STATE-002]
---

## Summary

The score measures progress toward the scenario's goal: cash in Greed, sectors
held in Power, Big 40 and Armageddon, the Support of completed sites in
Acceptance, a weighted mix in Dominance, headquarters held in Siege,
eliminated opponents in Kill 'Em All and Eliminate, and points gathered over
the match in Big Man. A player's
standing is how many players have a strictly higher score, so tied players
share a standing and the best is 0. Eliminated players have no standing.

## When it runs

At the start of RULE-OBJECTIVE-001, at the end of every turn. The computer
players and the Player Rankings panel read its results.

## Parameters

None.

## Inputs

`scenario`, `player_active`, `cash`, each sector's `owner` and `sites`, the
site definitions' `resistance` and `support` (FMT-DATA-001), `hq_sectors`,
`turn_limit` (Dominance), the previous `scenario_score` (Big Man only).

## Procedure

```text
# The Support of every completed site in the player's sectors, read from the
# site definitions at the moment of the evaluation
define completed_site_support(player) -> INT32:
    let n = 0
    for s in 0..64:
        if sectors[s].owner == player:
            for k in 0..3:
                let site = sectors[s].sites[k]
                let d = site_definitions[site.definition]
                if d.resistance == site.progress:
                    n = n + d.support
    return n

define owned_sector_count(player) -> INT32:
    let n = 0
    for s in 0..64:
        if sectors[s].owner == player:
            n = n + 1
    return n

let inactive = 0
for each player in turn_order:
    if not player_active[player]:
        inactive = inactive + 1

for each player in turn_order:
    if not player_active[player]:
        scenario_score[player] = -32000
    else if scenario == 8:
        # Big Man keeps the score it had and adds the centre sectors held now
        for each s in [27, 28, 35, 36]:
            if sectors[s].owner == player:
                scenario_score[player] = scenario_score[player] + 1
    else if scenario == scenario_greed:
        scenario_score[player] = cash[player]
    else if scenario == scenario_power or scenario == scenario_big_40 or scenario == 9:
        scenario_score[player] = owned_sector_count(player)
    else if scenario == scenario_acceptance:
        scenario_score[player] = completed_site_support(player)
    else if scenario == scenario_dominance:
        scenario_score[player] = dominance_points(player) / 10
    else if scenario == 6:
        # Siege: headquarters sectors held
        let held = 0
        for k in 0..6:
            if sectors[hq_sectors[k]].owner == player:
                held = held + 1
        scenario_score[player] = held
    else:
        # Kill 'Em All (4) and Eliminate (7)
        scenario_score[player] = inactive

for each player in turn_order:
    if player_active[player]:
        let above = 0
        for other in 0..6:
            if scenario_score[other] > scenario_score[player]:
                above = above + 1
        scenario_standing[player] = above
    else:
        scenario_standing[player] = 0xFF
```

## Outputs

No return value. Sets `scenario_score` and `scenario_standing` of all six
players. Makes no draws.

## Edge cases

Inactive players keep the score -32000 while the standings are counted, so an
active player whose score is below -32000 (in Greed, a debt of more than
$32,000) counts every inactive player as above it (BUG-OBJECTIVE-001). In
Kill 'Em All and Eliminate every active player has the same score, so all
share standing 0. Acceptance and Dominance count a site's Support from the
moment its progress reaches its Resistance, without waiting for the sector's
own `support` field to be refreshed before the next planning. Dominance divides with truncation toward zero, so a negative
numerator rounds toward zero.

## What the sources say

SRC-MANUAL-GOG, pages 12 to 14, gives the scores of the timed scenarios:
Greed by cash, Power by sectors controlled, Acceptance by Support from
influenced sites, and Dominance by cash, Support and sectors with weights that
depend on the time limit. It says the score is not cumulative; the executable
keeps a running total in Big Man, which page 14 describes as points collected
each turn. The manual gives no division by ten for Dominance.

## Differences between builds

None known.

## Open questions

- The scorer sets every score to 0 first (except in Big Man), fills in the
  active players, then gives the inactive ones -32000; the procedure writes
  the same result in one pass. Big Man scores are set to 0 when a match
  starts (FND-SETUP-015).
