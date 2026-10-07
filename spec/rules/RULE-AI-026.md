---
id: RULE-AI-026
title: Family-7 computer gangs sit where sites add the most Research, influence Research sites and research items in a fixed cycle
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-035, FND-AI-033, FND-AI-021, FND-AI-015, FND-AI-028, FND-AI-045, FND-AI-044, FND-EXE-004, FND-AI-054, FND-AI-055, FND-AI-042, FND-AI-060, EXP-TURN-020, FND-AI-074, EXP-TURN-050, EXP-TURN-057, EXP-TURN-073, EXP-TURN-083, EXP-TURN-088, FND-AI-001, FND-AI-006, FND-AI-013, FND-AI-019, FND-AI-024, FND-AI-039, FND-AI-047, FND-CONTROL-001, FND-HIRE-002, FND-PLATFORM-003, FND-RESEARCH-001, FND-RESEARCH-002, FND-STATE-004, FND-STATE-006, FND-STATE-007, FND-STATE-011, FND-TURN-001, FND-TURN-006, FND-UI-035, FND-UI-036, FND-AI-078, EXP-TURN-010, EXP-TURN-021, EXP-TURN-039, EXP-TURN-043, EXP-TURN-049, EXP-TURN-109]
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

# Research the item the previous target byte names, whatever the previous action
# was, while research on it remains; otherwise the next category after its type
# (FND-AI-078). The item, or -1 when the scan finds none, becomes the focus
define research_continuation(player, idx):
    let r = planning_records[idx]
    let last = r.previous_target
    let item = -1
    if research_remaining[last * 6 + player] != 0:
        item = last
    else:
        let want = 2
        let t = item_definitions[last].type
        if t == 2:
            want = 1
        else if t == 1:
            want = 3
        else if t == 3:
            want = -1
        item = research_first(player, idx, want)
    plan(idx, ACTION_RESEARCH, item, 0)
    aux_records[idx].focus = item

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
    let t = draw_once(player, idx, kind, idx % 81)
    if t != -1 and attitude[player * 6 + t / 81] < 0:
        plan(idx, ACTION_ATTACK, t / 81, t % 81)
        aux_records[idx].focus = s
        done = true
else if danger_near(player, idx):
    # Equip leaves the focus as it was (FND-AI-074)
    let wp = weapon_upgrade(player, idx)
    let ar = armor_upgrade(player, idx)
    if wp != -1 and r.weapon_cooldown <= 0:
        plan(idx, ACTION_EQUIP, wp, 0)
        r.weapon_cooldown = item_definitions[wp].cost * 3
        done = true
    else if ar != -1 and r.armor_cooldown <= 0:
        plan(idx, ACTION_EQUIP, ar, 0)
        r.armor_cooldown = item_definitions[ar].cost * 3
        done = true
if not done:
    if prev == ACTION_EQUIP or prev == ACTION_MOVE or prev == ACTION_ATTACK or prev == ACTION_INFLUENCE:
        r.previous_target = 0
    if g.force < 8 and g.heal >= -3:
        plan(idx, ACTION_HEAL, 0, 0)
    else:
        let best = s
        for c in 0..64:
            if sectors[c].owner == player and research_score(c) > research_score(best):
                best = c
        # A focus equal to the best sector skips the Move and the Influence (FND-AI-078);
        # best is never -1 for an active gang
        if aux_records[idx].focus == best or best == -1:
            research_continuation(player, idx)
        else if best != s:
            plan(idx, ACTION_MOVE, select_sector(player, best + 0x40, idx), 0)
            aux_records[idx].focus = -1
        else:
            let influenced = false
            for k in 0..3:
                if site_definitions[sectors[s].sites[k].definition].research > 0 and site_unfinished(s, k):
                    plan(idx, ACTION_INFLUENCE, k, 0)
                    influenced = true
                    break
            if not influenced:
                aux_records[idx].focus = s
                research_continuation(player, idx)
    # Every branch above ends here; only a Research that found no item leaves -1
    if r.planned_target == -1:
        let item = -1
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
previous action, and may set the family to 0. An Equip leaves `focus` as the
previous pass left it and sets the matching cooldown to three times the item's
cost; it is considered only when the sector's weight is not 10. Draws one `roll` for the target draw
at weight 10, plus the draws inside `select_sector`.

## Edge cases

The research sector starts as the current sector even when the player does not
own it, and an owned sector replaces it only with a strictly greater sum, so a
gang in an unowned sector with a sum of 0 or more stays unless an owned sector
beats it. A failed attack draw, or a successful one on a gang whose player the
computer is not hostile to, falls through to the research sequence. Item 0 is
never researched through the type scans.

The focus test runs before the Move and the Influence. A gang whose focus
equals the best research sector researches where it stands, even outside that
sector and even when its sector has a Research site left to influence. The
focus holds the gang's sector after an Attack, -1 after a Move and an item
number after a Research, so an item number equal to the best sector's number
passes the test too. An Equip, a Heal and an Influence leave the focus of the
pass before (FND-AI-074, FND-AI-078), so the test after one of them compares
what that pass stored.

The research continuation reads the previous target byte without looking at
the previous action. After a previous Equip, Move, Attack or Influence the
byte has just been cleared, so the gang tests item 0's research byte. A byte
that survives from another action is read as an item: an Influence the
duplicate cleanup of
RULE-AI-001 rewrote to Snitch keeps its site slot, so the gang tests the
research byte of item 0, 1 or 2 and, when it is 0, asks for the category after
that item's type.

The fallback scans run when the planned target is -1 after the branch, which
only a Research whose scan found nothing leaves: a Heal leaves 0, an
Influence a site slot and a Move a sector (RULE-AI-006).

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

- EXP-TURN-020 directly checks the successful weight-10 attack branch: call
  10549 draws the sole human target and the attack resolves. The fixture
  compares the complete draw stream and final state. EXP-TURN-073 reaches the
  fallback scans and the fixed item list, and a gang that finds no item to
  research, takes family 0 and moves through selector mode 5. EXP-TURN-010's
  second run, EXP-TURN-039, EXP-TURN-043 and EXP-TURN-049's first run reach
  the focus test (FND-AI-078) with the gang outside its best research sector
  and a focus equal to it, and the gang researches in place; without the test
  all four replays part from the original. EXP-TURN-021 and EXP-TURN-049 reach
  the research continuation after a Snitch the duplicate cleanup wrote, whose
  previous target is a site slot, and part from the original when the
  continuation tests the previous action. EXP-TURN-109 reaches the focus test
  with the gang in its best sector and a Research site there unfinished; the
  gang researches, and its replay parts from the original when it influences
  the site instead. Its next pass, with the item number in the focus,
  influences that site. No recorded run isolates the failed or non-hostile
  attack fallthrough, weapon and armor upgrade cooldowns, Heal,
  best-Research-sector routing and ties, Research-site Influence, type
  cycling, or the late Greed Terminate override, so the research procedure is
  not established.
- The item `type` numbers (0 melee, 1 blade, 2 ranged, 3 armor, 4
  miscellaneous) are assumptions shared with RULE-AI-005.
- A previous Research of an item of type 4 that is not on the fixed list, or of
  melee, next asks for ranged, as FND-AI-035 states for "every other type".
