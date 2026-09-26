---
id: RULE-AI-005
title: How a computer player picks a weapon, armor or miscellaneous upgrade, and when danger calls for one
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-021, FND-AI-024, FND-AI-037, FND-AI-039, FND-AI-010, FND-AI-013, FND-EXE-004, FND-AI-054, FND-AI-055]
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
`tech_level`, `combat`, `defense`, `stealth`, `detect`, `control`),
`research_remaining`, `cash`, `local_tech_cap`,
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

# The first item of type k the gang may buy under its local Tech ceiling, or 0
define first_affordable(player, idx, k):
    for item in 0..64:
        let it = item_definitions[item]
        if it.type == k and it.tech_level <= local_tech_cap[idx] and research_remaining[item * 6 + player] == 0 and it.cost <= cash[player]:
            return item
    return 0

# The weapon upgrade for the gang idx, or -1
define weapon_upgrade(player, idx):
    let g = gangs[idx]
    let bare = g.strength + g.fighting + g.martial_arts
    let s1 = item_definitions[first_affordable(player, idx, 1)].combat + g.strength + g.blade
    let s0 = item_definitions[first_affordable(player, idx, 0)].combat + g.strength
    let s2 = item_definitions[first_affordable(player, idx, 2)].combat + g.ranged
    if s1 < bare and s0 < bare and s2 < bare:
        return -1
    # the greatest score; ranged wins every tie, melee wins a tie with blade
    let k = 2
    if s0 >= s1 and s0 > s2 and s0 >= bare:
        k = 0
    else if s1 > s0 and s1 > s2 and s1 >= bare:
        k = 1
    let own_tech = gang_definitions[g.definition].tech_level
    let choice = g.weapon
    if choice == -1:
        choice = 24
    for item in 0..64:
        let it = item_definitions[item]
        if it.type == k and research_remaining[item * 6 + player] == 0 and it.tech_level <= own_tech:
            if it.combat > item_definitions[choice].combat and it.cost <= cash[player]:
                choice = item
    if choice == 24 or choice == g.weapon:
        return -1
    return choice

# The armor upgrade by Defense, within cash (selector 0x64)
define armor_upgrade(player, idx):
    let g = gangs[idx]
    let own_tech = gang_definitions[g.definition].tech_level
    let choice = g.armor
    if choice == -1:
        choice = 0
    for item in 0..64:
        let it = item_definitions[item]
        if it.type == 3 and research_remaining[item * 6 + player] == 0 and it.tech_level <= own_tech:
            if it.defense > item_definitions[choice].defense and it.cost < cash[player]:
                choice = item
    if choice == 0 or choice == g.armor:
        return -1
    return choice

# The armor upgrade by Stealth, ignoring cash (selector 0x72, family 10)
define armor_stealth_upgrade(player, idx):
    let g = gangs[idx]
    let own_tech = gang_definitions[g.definition].tech_level
    let choice = g.armor
    if choice == -1:
        choice = 1
    for item in 0..64:
        let it = item_definitions[item]
        if it.type == 3 and research_remaining[item * 6 + player] == 0 and it.tech_level <= own_tech:
            if it.stealth > item_definitions[choice].stealth:
                choice = item
    if choice == 1 or choice == g.armor:
        return -1
    return choice

# The miscellaneous item upgrade by Detect, ignoring cash (selector 0x74)
define misc_detect_upgrade(player, idx):
    let g = gangs[idx]
    let own_tech = gang_definitions[g.definition].tech_level
    let choice = g.misc
    if choice == -1:
        choice = 0
    for item in 0..64:
        let it = item_definitions[item]
        if it.type == 4 and research_remaining[item * 6 + player] == 0 and it.tech_level <= own_tech:
            if it.detect > item_definitions[choice].detect:
                choice = item
    if choice == 0 or choice == g.misc:
        return -1
    return choice

# The miscellaneous item upgrade by Control, ignoring cash (selector 0x75)
define misc_control_upgrade(player, idx):
    let g = gangs[idx]
    let own_tech = gang_definitions[g.definition].tech_level
    let choice = g.misc
    if choice == -1:
        choice = 0
    for item in 0..64:
        let it = item_definitions[item]
        if it.type == 4 and research_remaining[item * 6 + player] == 0 and it.tech_level <= own_tech:
            if it.control > item_definitions[choice].control:
                choice = item
    if choice == 0 or choice == g.misc:
        return -1
    return choice
```

## Outputs

`danger_near` returns true or false. `first_affordable` returns an item record
number. `weapon_upgrade`, `armor_upgrade`, `armor_stealth_upgrade`,
`misc_detect_upgrade` and `misc_control_upgrade` return an item record number
or -1. None of them changes state or draws from `rng`.

## Edge cases

Bare hands win the class comparison only when every weapon class scores less
than Strength + Fighting + Martial Arts, and then `weapon_upgrade` returns -1.
A class with no item the gang may buy is scored with item 0's Combat. The first
class pass caps an item's Tech with `local_tech_cap` and needs the full cost in
cash, and the second pass caps it with the gang's own Tech, so the item finally
chosen can differ from the one that won its class. Every upgrade must beat the
equipped item; an empty slot is compared with item 24 (weapon), item 0 (armor
by Defense, miscellaneous) or item 1 (armor by Stealth), and choosing that item
itself counts as no upgrade. Only the weapon and the Defense armor test cash,
the weapon with cost at most cash and the armor with cost below it. Among equal
values the first item in record order wins. `danger_near` reads index 64 past
the last sector (BUG-AI-002).

## What the sources say

SRC-MANUAL-GOG does not describe how computer players choose equipment.

## Differences between builds

None known.

## Open questions

- Whether `danger_near` scans the centre cell with its neighbours is assumed.
- `combat_records[0].definition` is the disputed byte 0 of FMT-STATE-003;
  either reading gives 0 for player 0's Right Hands (FND-AI-010).
