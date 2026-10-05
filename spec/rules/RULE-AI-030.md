---
id: RULE-AI-030
title: Family-12 computer gangs equip and heal when unopposed, step toward their player's first gang, and attack when opposed
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-073, EXP-TURN-013, FND-AI-070, FND-AI-033, FND-AI-069, FND-AI-040, FND-EXE-004, FND-OBJECTIVE-003, FND-AI-055, FND-AI-042, FND-AI-075, EXP-TURN-053, EXP-TURN-057, EXP-TURN-080]
conflicting: []
split_with: []
related: [RULE-AI-004, RULE-AI-005, RULE-AI-006, RULE-RNG-002, FMT-STATE-001, FMT-STATE-002]
---

## Summary

Family 12 is an Eliminate skirmisher (scenario 7). With no other gang in sight
a family-12 gang buys a weapon, armor or a Detect item, heals when hurt, and
otherwise takes one step toward the sector of its player's first gang, in
roster slot 0, which in Eliminate is the Right Hands.
With a gang in sight it attacks after up to five draws. In Greed, during the
last three turns, it terminates instead.

## When it runs

From RULE-AI-002, for a gang whose family is 12.

## Parameters

`player`: the computer player. `slot`: the gang's roster slot.

## Inputs

The gang's record in `gangs` (`sector`, `force`, `heal`) and in
`planning_records` (`weapon_cooldown`, `armor_cooldown`), `sector_weight`,
`sectors` (`owner`), `item_definitions` (`cost`), `cash`, `scenario`,
`rng_state` through `roll`, and what the functions it calls read.

## Procedure

```text
let idx = player * 81 + slot
let g = gangs[idx]
let r = planning_records[idx]
let s = g.sector
let w = sector_weight[player * 64 + s]
if w == 0:
    let wp = weapon_upgrade(player, idx)
    let ar = armor_upgrade(player, idx)
    let mi = misc_detect_upgrade(player, idx)
    if wp != -1 and r.weapon_cooldown <= 0:
        plan(idx, ACTION_EQUIP, wp, 0)
        r.weapon_cooldown = item_definitions[wp].cost
        aux_records[idx].focus = -1
    else if ar != -1 and r.armor_cooldown <= 0:
        plan(idx, ACTION_EQUIP, ar, 0)
        r.armor_cooldown = item_definitions[ar].cost
        aux_records[idx].focus = -1
    else if mi != -1 and item_definitions[mi].cost <= cash[player]:
        plan(idx, ACTION_EQUIP, mi, 0)
        aux_records[idx].focus = -1
    else if g.force < 10 and g.heal >= -3:
        plan(idx, ACTION_HEAL, 0, 0)
        aux_records[idx].focus = -1
    else:
        # selector 0x5A for roster slot 0, not for this gang [FND-AI-070]
        let first = gangs[player * 81].sector
        let dest = select_sector(player, first + 0x40, idx)
        plan(idx, ACTION_MOVE, dest, 0)
        aux_records[idx].focus = -1
        aux_records[idx].coverage_sector = dest
else:
    let kind = 0
    if w == 10 and hostile_human_owner(player, s):
        kind = 1
    let t = draw_target(player, idx, kind, 5)
    plan(idx, ACTION_ATTACK, t / 81, t % 81)
    aux_records[idx].focus = s
if scenario == 0 and turns_remaining() < 4:
    plan(idx, ACTION_TERMINATE, 0, 0)
    r.needs_family = 1
```

## Outputs

No return value. Writes the gang's planned action and targets through `plan`
(Equip targets the item, Attack the drawn gang's player and roster slot, Move
the sector `select_sector` returns). A weapon or armor Equip sets the matching
cooldown to the item's cost. Sets `focus` to the gang's sector after an Attack
and to -1 after every other action, and `coverage_sector` to a Move's
destination (FND-AI-075). Draws up to five `roll`s in the attack loop, or
the draws inside `select_sector` for the step toward the first gang.

## Edge cases

The Move passes the sector of the gang in roster slot 0 as an encoded mode,
so the selector scores only that sector and steps toward it (RULE-AI-006). A
gang standing in that sector, the first gang itself included, has its score
removed with the source sector's; every pair then ties at 0, the tie count
runs on past the pair list, and the draw picks the sector to step toward
(RULE-AI-006). When roster slot 0 is empty its sector byte is 100, and the
mode is the guard end marker `0x40 + 100`. In a sector owned by a hostile
human where the first visible gang belongs to a computer player, the weight
is 1 and the draw takes every visible gang (EXP-TURN-080). The human-only pool
is drawn only at weight 10, which needs a visible gang of a human, so it is
never empty. The attack loop attacks the
last drawn target even when all five strength tests failed, and the strength
test can be made on a different gang from the one attacked (BUG-AI-003).

## What the sources say

SRC-MANUAL-GOG does not describe the computer players' strategies.

## Differences between builds

None known.

## Open questions

- That "no visible opponent" is a cached weight of 0 is taken from the
  handler's use of that weight (selector `0xAF`).
