---
id: RULE-AI-027
title: Family-9 computer gangs equip without waiting, leave owned land, and fight or take other players' sectors
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-036, FND-AI-038, FND-AI-033, FND-AI-028, FND-AI-043, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-AI-001, BUG-AI-005, RULE-AI-004, RULE-AI-005, RULE-AI-006, RULE-RNG-002, FMT-STATE-001, FMT-STATE-002]
---

## Summary

Family 9 is a raider. A family-9 gang buys a better weapon or armor whenever
one is available, without the waiting time other families keep. Otherwise it
leaves land its player owns through sector selector mode 3. In other land it
attacks when it sees a hostile human gang, and otherwise takes the sector by
Control, moving on after a Control.

## When it runs

From RULE-AI-002, for a gang whose family is 9. Only a player whose
`raider_mode` is set has family-9 gangs. It is set when a network player's
connection is lost and a computer player takes the slot over: at that moment
the game switches the slot's controller to a computer, resets its planning
records, writes family 9 to every active gang's record and runs its planning
pass at once. From then on RULE-AI-001 sets family 9 on every active gang at
every pass, so later hires are raiders too. The flag is kept in saves and
cleared when a new match starts.

## Parameters

`player`: the computer player. `slot`: the gang's roster slot.

## Inputs

The gang's record in `gangs` (`sector`) and in `planning_records`
(`previous_action`), `sector_weight`, `sectors` (`owner`), `item_definitions`
(`cost`), `rng_state` through `roll`, and what the functions it calls read.

## Procedure

```text
let idx = player * 81 + slot
let g = gangs[idx]
let r = planning_records[idx]
let s = g.sector
let w = sector_weight[player * 64 + s]
let wp = weapon_upgrade(player, idx)
let ar = armor_upgrade(player, idx)
if wp != -1:
    plan(idx, ACTION_EQUIP, wp, 0)
    r.weapon_cooldown = item_definitions[wp].cost * 3
else if ar != -1:
    plan(idx, ACTION_EQUIP, ar, 0)
    r.armor_cooldown = item_definitions[ar].cost * 3
else if sectors[s].owner == player:
    plan(idx, ACTION_MOVE, select_sector(player, 3, idx), 0)
else if w == 10:
    let kind = 0
    if hostile_human_owner(player, s):
        kind = 1
    let t = draw_target(player, idx, kind, 5)
    plan(idx, ACTION_ATTACK, t / 81, t % 81)
else if r.previous_action == ACTION_CONTROL:
    plan(idx, ACTION_MOVE, select_sector(player, 3, idx), 0)
else:
    plan(idx, ACTION_CONTROL, 0, 0)
```

## Outputs

No return value. Writes the gang's planned action and targets through `plan`
(Equip targets the item, Attack the drawn gang's player and roster slot, Move
the sector `select_sector` returns). An Equip overwrites the matching cooldown
with three times the item's cost. Draws up to five `roll`s in the attack loop,
plus the draws inside `select_sector`.

## Edge cases

The cooldown is written but never read by this family, so a family-9 gang can
equip every turn while upgrades are affordable. It never heals and never
terminates in Greed. It attacks the last drawn target even when all five
strength tests failed, and the strength test can be made on a different gang
from the one attacked (BUG-AI-003). It tries Control without testing whether it
can take the sector alone.

## What the sources say

SRC-MANUAL-GOG does not describe the computer players' strategies.

## Differences between builds

None known.

## Open questions

- The pool choice for the draws is taken from family 12 (FND-AI-038), as
  FND-AI-036 says it is the same.
