---
id: RULE-AI-005
title: How a computer player picks a weapon, armor or miscellaneous upgrade, and when danger calls for one
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-021, FND-AI-024, FND-AI-037, FND-AI-039, FND-AI-010, FND-AI-013]
conflicting: []
split_with: []
related: [FMT-STATE-001, FMT-STATE-002, FMT-STATE-003, RULE-AI-004]
---

## Summary

A computer gang upgrades its equipment when enemies are close: it looks for
the best weapon for its fighting style, an armor with more Defense, or a
miscellaneous item with more Chaos, among the items its player has researched
and can pay for.

## When it runs

Whenever a family handler calls one of its functions, during `planning_phase`.

## Parameters

None.

## Inputs

`gangs` (the gang's `weapon`, `armor`, `misc`, `sector`, `definition`,
`strength`, `blade`, `ranged`, `fighting` and `martial_arts`),
`gang_definitions` (`tech_level`), `item_definitions` (`type`, `cost`,
`tech_level`, `combat`, `defense`, `chaos`), `research_remaining`, `cash`,
`scenario`, `sectors`, `sector_weight` and `combat_records`.

## Procedure

```text
# The owner the neighbourhood scans read at a linear sector index c from 0 to 64
define owner_at(c):
    # index 64 lies past the last sector: the original reads the first byte
    # of the combat records there, which holds 0 for player 0's Right Hands
    if c == 64:
        return combat_records[0].definition
    return sectors[c].owner

# The cached weight the neighbourhood scans read for player at index c
define weight_at(player, c):
    # at index 64 the flat array gives the next player's sector 0, and 0 for player 5
    if c == 64:
        if player == 5:
            return 0
        return sector_weight[(player + 1) * 64]
    return sector_weight[player * 64 + c]

# Whether an item is researched for the player, within a Tech cap and affordable
define item_ok(player, item, tech_cap):
    let it = item_definitions[item]
    return research_remaining[item * 6 + player] == 0 and it.tech_level <= tech_cap and it.cost <= cash[player]

# Whether the gang idx sees danger near it: the equipment gate
define danger_near(player, idx):
    let g = gangs[idx]
    let center = g.sector
    let cx = center % 8
    if owner_at(center) == player and weight_at(player, center) == 10:
        return true
    if g.weapon != -1 and g.armor != -1:
        return false
    for dy in -1..2:
        for dx in -1..2:
            let c = center + dy * 8 + dx
            if c < 0 or c >= 65:
                continue
            if cx + dx < 0 or cx + dx > 7:
                continue
            let o = owner_at(c)
            if scenario == 0:
                if weight_at(player, c) == 10 and o == player:
                    return true
            else if (o >= 0 and o != player) or weight_at(player, c) == 10:
                return true
    return false

# The weapon upgrade for the gang idx, or -1
define weapon_upgrade(player, idx):
    let g = gangs[idx]
    let cap = local_tech_cap[idx]
    # classes in the order that wins ties: ranged, melee, blade
    let classes = [2, 0, 1]
    let best_class = -1
    let best = 0
    let baseline = 0
    for each k in classes:
        let first = -1
        for item in 0..64:
            if item_definitions[item].type == k and item_ok(player, item, cap):
                first = item
                break
        if first == -1:
            continue
        let score = item_definitions[first].combat + g.strength
        if k == 1:
            score = score + g.blade
        if k == 2:
            score = item_definitions[first].combat + g.ranged
        if best_class == -1 or score > best:
            best = score
            best_class = k
            baseline = item_definitions[first].combat
    # bare hands win only on a strictly greater score
    if best_class == -1 or g.strength + g.fighting + g.martial_arts > best:
        return -1
    let own_tech = gang_definitions[g.definition].tech_level
    let choice = -1
    for item in 0..64:
        if item_definitions[item].type == best_class and item_ok(player, item, own_tech):
            if item_definitions[item].combat > baseline:
                baseline = item_definitions[item].combat
                choice = item
    if choice == g.weapon:
        return -1
    return choice

# The armor upgrade for the gang idx, or -1
define armor_upgrade(player, idx):
    let g = gangs[idx]
    let own_tech = gang_definitions[g.definition].tech_level
    let current = 0
    if g.armor != -1:
        current = item_definitions[g.armor].defense
    let choice = -1
    for item in 0..64:
        let it = item_definitions[item]
        if it.type == 3 and research_remaining[item * 6 + player] == 0 and it.tech_level <= own_tech and it.cost < cash[player]:
            if it.defense > current:
                current = it.defense
                choice = item
    return choice

# The armor with the greatest Defense improvement, ignoring cash (family 10)
define armor_defense_upgrade(player, idx):
    let g = gangs[idx]
    let own_tech = gang_definitions[g.definition].tech_level
    let base = g.armor
    if base == -1:
        base = 1
    let current = item_definitions[base].defense
    let choice = -1
    for item in 0..64:
        let it = item_definitions[item]
        if it.type == 3 and research_remaining[item * 6 + player] == 0 and it.tech_level <= own_tech:
            if it.defense > current:
                current = it.defense
                choice = item
    return choice

# The miscellaneous item with the greatest Chaos improvement, ignoring cash
define misc_chaos_upgrade(player, idx):
    let g = gangs[idx]
    let own_tech = gang_definitions[g.definition].tech_level
    let base = g.misc
    if base == -1:
        base = 0
    let current = item_definitions[base].chaos
    let choice = -1
    for item in 0..64:
        let it = item_definitions[item]
        if it.type == 4 and research_remaining[item * 6 + player] == 0 and it.tech_level <= own_tech:
            if it.chaos > current:
                current = it.chaos
                choice = item
    return choice
```

## Outputs

`danger_near` returns true or false. `weapon_upgrade`, `armor_upgrade`,
`armor_defense_upgrade` and `misc_chaos_upgrade` return an item record number
or -1. None of them changes state or draws from `rng`.

## Edge cases

Bare hands win the class comparison when no weapon class scores more than
Strength + Fighting + Martial Arts, and then `weapon_upgrade` returns -1. The
first class pass caps an item's Tech with `local_tech_cap`, and the second
pass caps it with the gang's own Tech, so the item finally chosen can differ
from the one that won its class. `danger_near` reads index 64 past the last
sector (BUG-AI-002).

## What the sources say

SRC-MANUAL-GOG does not describe how computer players choose equipment.

## Differences between builds

None known.

## Open questions

- `local_tech_cap` stands for the Tech ceiling selector `0x62` computes for a
  gang from the gang and the sites near it; how it is computed is not
  described by the findings.
- The weapon class numbers used here (0 melee, 1 blade, 2 ranged) are
  assumptions; the item `type` values of the weapon classes are not given by
  the findings. Armor is type 3 and miscellaneous items type 4.
- The second pass keeps an item only when its Combat bonus is strictly greater
  than the baseline, the Combat bonus of the first eligible item of the winning
  class. FND-AI-024 says the selector returns -1 when "no upgrade beats the
  baseline", which this reading follows; whether the baseline is instead the
  equipped weapon's bonus is not settled.
- `armor_upgrade` (selector `0x64`) and `misc_chaos_upgrade` (selectors `0x74`
  and `0x75`): the findings give their conditions but not which candidate
  wins when several qualify. The first strictly greatest value in item order is
  assumed, as selector `0x72` does.
- Whether the item 0 and item 1 baselines used here index the item table (the
  findings call them the empty-slot and zero-Defense baselines) is assumed.
- Whether `danger_near` scans the centre cell with its neighbours is assumed.
- `combat_records[0].definition` is the disputed byte 0 of FMT-STATE-003;
  either reading gives 0 for player 0's Right Hands (FND-AI-010).
