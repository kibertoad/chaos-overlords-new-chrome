---
id: RULE-AI-030
title: Family-12 computer gangs equip and heal when unopposed, wander at random, and attack when opposed
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-038, FND-AI-033, FND-AI-027, FND-AI-040, FND-EXE-004, FND-OBJECTIVE-003, FND-AI-055]
conflicting: []
split_with: []
related: [RULE-AI-004, RULE-AI-005, RULE-AI-006, RULE-RNG-002, FMT-STATE-001, FMT-STATE-002]
---

## Summary

Family 12 is an Eliminate skirmisher (scenario 7). With no other gang in sight
a family-12 gang buys a weapon, armor or a Detect item, heals when hurt, and
otherwise takes one step toward a sector drawn at random from the whole map.
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
    else if ar != -1 and r.armor_cooldown <= 0:
        plan(idx, ACTION_EQUIP, ar, 0)
        r.armor_cooldown = item_definitions[ar].cost
    else if mi != -1 and item_definitions[mi].cost <= cash[player]:
        plan(idx, ACTION_EQUIP, mi, 0)
    else if g.force < 10 and g.heal >= -3:
        plan(idx, ACTION_HEAL, 0, 0)
    else:
        plan(idx, ACTION_MOVE, select_sector(player, s + 0x40, idx), 0)
else:
    let kind = 0
    if w == 10 and hostile_human_owner(player, s):
        kind = 1
    let t = draw_target(player, idx, kind, 5)
    plan(idx, ACTION_ATTACK, t / 81, t % 81)
if scenario == 0 and turns_remaining() < 4:
    plan(idx, ACTION_TERMINATE, 0, 0)
```

## Outputs

No return value. Writes the gang's planned action and targets through `plan`
(Equip targets the item, Attack the drawn gang's player and roster slot, Move
the sector `select_sector` returns). A weapon or armor Equip sets the matching
cooldown to the item's cost. Draws up to five `roll`s in the attack loop, or
the draws inside `select_sector` for the random step.

## Edge cases

The Move passes the current sector as an encoded mode; the selector then
zeroes the current sector's score, all 64 sectors tie at 0, and `roll(64)`
picks the sector to step toward (RULE-AI-006). In a sector owned by a hostile
human where every visible gang belongs to a computer player, the human-only
pool is empty and `roll(0)` gives 1 (RULE-AI-004). The attack loop attacks the
last drawn target even when all five strength tests failed, and the strength
test can be made on a different gang from the one attacked (BUG-AI-003).

## What the sources say

SRC-MANUAL-GOG does not describe the computer players' strategies.

## Differences between builds

None known.

## Open questions

- What the original reads for the empty human-only pool is not recorded
  (FND-AI-038).
- That "no visible opponent" is a cached weight of 0 is taken from the
  handler's use of that weight (selector `0xAF`).
