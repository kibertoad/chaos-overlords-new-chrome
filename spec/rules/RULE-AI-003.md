---
id: RULE-AI-003
title: Each planning pass refreshes a computer player's gang counts, sector danger and combat-advantage hostility
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-018, FND-AI-040, FND-AI-039, FND-AI-013, FND-AI-019, FND-AI-004, FND-AI-006]
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
cleanup and the dispatch.

## Parameters

`player`: the computer player planning.

## Inputs

`gangs` (`player`, `sector`, `combat`, `defense`, `visible_to`), `sectors`
(`owner`), `controller`, `attitude` and `mentality`.

## Procedure

```text
for s in 0..64:
    sector_gang_count[player * 64 + s] = 0
for slot in 0..81:
    let g = gangs[player * 81 + slot]
    if g.sector != GANG_INACTIVE:
        sector_gang_count[player * 64 + g.sector] = sector_gang_count[player * 64 + g.sector] + 1
for s in 0..64:
    sector_weight[player * 64 + s] = visible_weight(player, s)
for o in 0..6:
    if o == player:
        continue
    combat_advantage[player * 6 + o] = 0
    let owned = 0
    let advantaged = 0
    for s in 0..64:
        if sectors[s].owner != o:
            continue
        owned = owned + 1
        let ours = 0
        let theirs = 0
        for slot in 0..81:
            let g = gangs[player * 81 + slot]
            if g.sector == s:
                ours = ours + g.combat + g.defense
            let d = gangs[o * 81 + slot]
            if d.sector == s and d.visible_to[player] == 1:
                theirs = theirs + d.combat + d.defense
        if ours > theirs:
            advantaged = advantaged + 1
    let eligible = false
    if is_human(o):
        eligible = mentality >= 1
    else:
        eligible = mentality < 2
    if eligible and owned > 0 and advantaged > 0 and advantaged * 100 / owned > 75:
        combat_advantage[player * 6 + o] = 1
        attitude[player * 6 + o] = -10
```

## Outputs

No return value. Rebuilds the player's row of `sector_gang_count` and of
`sector_weight`, and its row of `combat_advantage`. Sets
`attitude[player * 6 + o]` to -10 for each opponent `o` that passes the test.
Makes no draw.

## Edge cases

Exactly 75 percent does not qualify. A sector where the player has no gangs
counts as advantaged only if the visible defenders sum to less than 0. Hidden
defenders (visibility byte not 1) are left out, so the test can overrate the
player's strength. An opponent with no sectors is never made hostile here.

## What the sources say

SRC-MANUAL-GOG, numbered pages 14 and 15, calls the four Mentality settings
difficulty levels, from Goon (easiest) through Criminal (normal) and Crimelord
to Homicidal Maniac, which it advises against playing with only one human. It
does not say that the setting changes whom the computer players turn against.

## Differences between builds

None known.

## Open questions

- Whether `combat_advantage` is cleared when the test fails, or keeps the value
  of an earlier turn, is not recorded; the procedure clears it.
- The order of the three parts (gang counts, sector weights, pair records)
  inside `0x0040A1A7` is not recorded; none of them reads another's result
  except through `visible_weight`, which reads `attitude`, so a hostility set
  in the pair part can change the weights only if the pair part comes first.
- Whether the sector weight cache is refreshed here or by another part of the
  planner is inferred from FND-AI-039, which says the refresh caches selector
  `0x90` per player and sector.
- The Mentality test for a player slot whose controller is -1 (empty) is not
  recorded; such a player owns no sectors.
- The addresses of `sector_weight` and `combat_advantage` are not recorded.
