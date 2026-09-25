---
id: RULE-AI-003
title: Each planning pass refreshes a computer player's gang counts, sector danger and combat-advantage hostility
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-044, FND-AI-018, FND-AI-040, FND-AI-039, FND-AI-013, FND-AI-019, FND-AI-004, FND-AI-006, FND-AI-045, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-AI-004, FMT-STATE-001, FMT-STATE-002]
---

## Summary

Before its gangs choose their orders, a computer player counts its gangs in
each sector, notes how dangerous each sector looks, and checks every opponent's
territory. If its gangs already out-fight the defenders it can see in more than
three quarters of an opponent's sectors, it turns fully hostile to that
opponent. At Goon it does this only to computer players, at Criminal to
anyone, and at Crime Lord and Homicidal Maniac only to humans.

## When it runs

In RULE-AI-001, after the action history is rolled and before the duplicate
cleanup and the dispatch. It also runs once for each player when a match
starts or is loaded, from the step that caches the site sums (RULE-AI-026).

## Parameters

`player`: the computer player planning.

## Inputs

`gangs` (`player`, `sector`, `combat`, `defense`, `visible_to`), `sectors`
(`owner`), `planning_records` (`needs_family`), `controller`, `attitude` and
`mentality`.

## Procedure

```text
# every observer's pair records are cleared, not only this player's row
for q in 0..6:
    for o in 0..6:
        combat_advantage[q * 6 + o] = 0
active_gang_count[player] = 0
let owned = [0, 0, 0, 0, 0, 0]
let advantaged = [0, 0, 0, 0, 0, 0]
for s in 0..64:
    sector_weight[player * 64 + s] = visible_weight(player, s)
    sector_gang_count[player * 64 + s] = 0
    let o = sectors[s].owner
    if o < 0 or o == player:
        continue
    owned[o] = owned[o] + 1
    let ours = 0
    let own_gangs = 0
    let theirs = 0
    for slot in 0..81:
        let g = gangs[player * 81 + slot]
        if g.sector == s:
            ours = ours + g.combat + g.defense
            own_gangs = own_gangs + 1
        let d = gangs[o * 81 + slot]
        if d.sector == s and d.visible_to[player] == 1:
            theirs = theirs + d.combat + d.defense
    if ours > theirs and own_gangs > 0:
        advantaged[o] = advantaged[o] + 1
for o in 0..6:
    if o == player:
        continue
    let eligible = false
    if is_human(o):
        eligible = mentality >= 1
    else:
        eligible = mentality < 2
    if eligible and owned[o] > 0 and advantaged[o] > 0 and advantaged[o] * 100 / owned[o] > 75:
        combat_advantage[player * 6 + o] = 1
        attitude[player * 6 + o] = -10
for slot in 0..81:
    let idx = player * 81 + slot
    let g = gangs[idx]
    if g.sector != GANG_INACTIVE:
        active_gang_count[player] = active_gang_count[player] + 1
        sector_gang_count[player * 64 + g.sector] = sector_gang_count[player * 64 + g.sector] + 1
        if planning_records[idx].needs_family != 0:
            aux_records[idx].focus = -1
            aux_records[idx].coverage_sector = -1
```

## Outputs

No return value. Rebuilds the player's row of `sector_gang_count` and of
`sector_weight` and its `active_gang_count`, clears every player's
`combat_advantage` and sets the player's own row, and sets both auxiliary
values of each flagged gang to -1. Sets
`attitude[player * 6 + o]` to -10 for each opponent `o` that passes the test.
Makes no draw.

## Edge cases

Exactly 75 percent does not qualify. A sector where the player has no gangs
never counts as advantaged. Hidden defenders (visibility byte not 1) are left
out, so the test can overrate the player's strength. An opponent with no
sectors is never made hostile here. Since the pass clears every player's
`combat_advantage` and sets only its own row, the other players' flags stay
clear until their own passes; no rule reads another player's row. The weights
are cached before the hostility test, so a player made hostile here changes the
weights only at the next pass.

## What the sources say

SRC-MANUAL-GOG, numbered pages 14 and 15, calls the four Mentality settings
difficulty levels, from Goon (easiest) through Criminal (normal) and Crimelord
to Homicidal Maniac, which it advises against playing with only one human. It
does not say that the setting changes whom the computer players turn against.

## Differences between builds

None known.

## Open questions

- The Mentality test for a player slot whose controller is -1 (empty) is not
  recorded; such a player owns no sectors.
