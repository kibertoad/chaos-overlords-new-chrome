---
id: RULE-AI-026
title: Family-7 computer gangs sit where sites add the most Research, influence Research sites and research items in a fixed cycle
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-035, FND-AI-033, FND-AI-021, FND-AI-015, FND-AI-028, FND-AI-045, FND-AI-044, FND-EXE-004, FND-AI-054, FND-AI-055, FND-AI-042, FND-AI-060]
conflicting: []
split_with: []
related: [RULE-AI-004, RULE-AI-005, RULE-AI-006, RULE-RNG-002, FMT-STATE-001, FMT-STATE-002, FMT-STATE-004]
---

## Summary

Family 7 is the researcher. A family-7 gang attacks a hostile gang it sees and
can beat, buys better equipment when danger is near, and heals when hurt.
Otherwise it moves to the owned sector whose sites add the most Research,
influences a Research site there, and then researches: ranged weapons, blade
weapons, armor and a fixed list of miscellaneous items in turn. When nothing is
left to research it joins family 0 and wanders. In Greed, during the last
three turns, it terminates instead.

## When it runs

From RULE-AI-002, for a gang whose family is 7.

## Parameters

`player`: the computer player. `slot`: the gang's roster slot.

## Inputs

The gang's record in `gangs` (`sector`, `force`, `heal`) and in
`planning_records` (`previous_action`, `previous_target`, `weapon_cooldown`,
`armor_cooldown`), `sector_weight`, `sectors` (`owner`, `sites`),
`site_definitions` (`research`), `item_definitions` (`type`, `tech_level`,
`cost`), `research_remaining`, `local_tech_cap`, `attitude`, `scenario`,
`rng_state` through `roll`, and what the functions it calls read.

## Procedure

```text
# The signed sum of the Research modifiers of the sites in sector c
define research_score(c):
    # read from a sum cached when the match starts or is loaded
    let n = 0
    for k in 0..3:
        n = n + site_definitions[sectors[c].sites[k].definition].research
    return n

# The first item still to research for the player: of the given type, or from
# the fixed list when want is -1; -1 when there is none
define research_first(player, idx, want):
    if want == -1:
        let list = [44, 41, 42, 43, 46, 50, 49, 52]
        for each item in list:
            if item_definitions[item].tech_level <= local_tech_cap[idx] and research_remaining[item * 6 + player] > 0:
                return item
        return -1
    for item in 1..64:
        let it = item_definitions[item]
        if it.type == want and it.tech_level <= local_tech_cap[idx] and research_remaining[item * 6 + player] > 0:
            return item
    return -1

let idx = player * 81 + slot
let g = gangs[idx]
let r = planning_records[idx]
let s = g.sector
let prev = r.previous_action
let w = sector_weight[player * 64 + s]
let done = false
if w == 10:
    let kind = 0
    if hostile_owner(player, s):
        kind = 1
    let t = draw_once(player, idx, kind)
    if t != -1 and attitude[player * 6 + t / 81] < 0:
        plan(idx, ACTION_ATTACK, t / 81, t % 81)
        aux_records[idx].focus = s
        done = true
if not done and danger_near(player, idx):
    let wp = weapon_upgrade(player, idx)
    let ar = armor_upgrade(player, idx)
    if wp != -1 and r.weapon_cooldown <= 0:
        plan(idx, ACTION_EQUIP, wp, 0)
        r.weapon_cooldown = item_definitions[wp].cost * 3
        aux_records[idx].focus = -1
        done = true
    else if ar != -1 and r.armor_cooldown <= 0:
        plan(idx, ACTION_EQUIP, ar, 0)
        r.armor_cooldown = item_definitions[ar].cost * 3
        aux_records[idx].focus = -1
        done = true
if not done:
    if prev == ACTION_EQUIP or prev == ACTION_MOVE or prev == ACTION_ATTACK or prev == ACTION_INFLUENCE:
        r.previous_target = 0
    if g.force < 8 and g.heal >= -3:
        plan(idx, ACTION_HEAL, 0, 0)
        done = true
if not done:
    let best = s
    for c in 0..64:
        if sectors[c].owner == player and research_score(c) > research_score(best):
            best = c
    if best != s:
        plan(idx, ACTION_MOVE, select_sector(player, best + 0x40, idx), 0)
        aux_records[idx].focus = -1
        done = true
if not done:
    for k in 0..3:
        if site_definitions[sectors[s].sites[k].definition].research > 0 and site_unfinished(s, k):
            plan(idx, ACTION_INFLUENCE, k, 0)
            done = true
            break
if not done:
    aux_records[idx].focus = s
    let item = -1
    if prev == ACTION_RESEARCH and research_remaining[r.previous_target * 6 + player] > 0:
        item = r.previous_target
    else:
        let want = 2
        if prev == ACTION_RESEARCH:
            let last = item_definitions[r.previous_target].type
            if last == 2:
                want = 1
            else if last == 1:
                want = 3
            else if last == 3:
                want = -1
        item = research_first(player, idx, want)
        if item == -1:
            for each fallback in [2, 1, 0, 3, -1]:
                item = research_first(player, idx, fallback)
                if item != -1:
                    break
    if item != -1:
        plan(idx, ACTION_RESEARCH, item, 0)
        aux_records[idx].focus = item
    else:
        r.family = 0
        plan(idx, ACTION_MOVE, select_sector(player, 5, idx), 0)
        aux_records[idx].focus = -1
if scenario == 0 and turns_remaining() < 4:
    plan(idx, ACTION_TERMINATE, 0, 0)
    r.needs_family = 1
```

## Outputs

`research_score` returns an integer and `research_first` an item record number
or -1. The handler returns nothing; it writes the gang's planned action and
targets through `plan` (Research and Equip target the item, Influence the site
slot, Move the sector `select_sector` returns, Attack the drawn gang's player
and roster slot), updates `focus`, may clear the first target byte of the
previous action, and may set the family to 0. An Equip sets the matching
cooldown to three times the item's cost. Draws one `roll` for the target draw
at weight 10, plus the draws inside `select_sector`.

## Edge cases

The research sector starts as the current sector even when the player does not
own it, and an owned sector replaces it only with a strictly greater sum, so a
gang in an unowned sector with a sum of 0 or more stays unless an owned sector
beats it. A failed attack draw, or a successful one on a gang whose player the
computer is not hostile to, falls through to the research sequence. Item 0 is
never researched through the type scans. A previous action whose target byte
was cleared reads as item 0 in the research continuation only when the
previous action was Research, which is never cleared.

`research_score` reads a sum the game caches for every sector when a match
starts or is loaded, not at each planning pass. It adds the Research of the
definitions in all three site slots without testing a site's progress or an
empty slot, and the cache is the same for every player. A change to a
sector's sites during a match is not seen until the match is loaded again.

## What the sources say

SRC-MANUAL-GOG does not describe the computer players' strategies.

## Differences between builds

None known.

## Open questions

- The item `type` numbers (0 melee, 1 blade, 2 ranged, 3 armor, 4
  miscellaneous) are assumptions shared with RULE-AI-005.
- A previous Research of an item of type 4 that is not on the fixed list, or of
  melee, next asks for ranged, as FND-AI-035 states for "every other type".
- The focus value is written as the current sector and then overwritten by the
  item number when Research is chosen (FND-AI-015); whether both writes happen
  is not recorded.
