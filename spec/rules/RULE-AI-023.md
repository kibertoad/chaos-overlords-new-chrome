---
id: RULE-AI-023
title: Family-4 computer gangs raise Chaos in owned land, probe weak enemies and move through sector selector mode 2, and no match reaches them
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-049, FND-AI-046, FND-AI-048, FND-AI-033, FND-AI-021, FND-AI-002, FND-AI-041, FND-AI-044, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-AI-004, RULE-AI-005, RULE-AI-006, RULE-RNG-002, FMT-STATE-001, FMT-STATE-002]
---

## Summary

Family 4 is a cautious family. A family-4 gang heals when hurt, raises Chaos
in land its player owns where no gang of its player did so last turn, tries to
attack a weak hostile gang it can see, and otherwise moves through sector
selector mode 2. It takes a sector by Control only after two moves in a row.
Nothing in the game writes family 4, so no match reaches this handler unless a
save already holds family 4 in a planning record.

## When it runs

From RULE-AI-002, for a gang whose family is 4.

## Parameters

`player`: the computer player. `slot`: the gang's roster slot.

## Inputs

The gang's record in `gangs` (`sector`, `force`, `heal`) and in
`planning_records` (`previous_action`, `older_action`, `weapon_cooldown`,
`armor_cooldown`), `sector_weight`, `sectors` (`owner`), `item_definitions`
(`cost`), `rng_state` through `roll`, and what the functions it calls read.

## Procedure

```text
let idx = player * 81 + slot
let g = gangs[idx]
let r = planning_records[idx]
let s = g.sector
let prev = r.previous_action
let w = sector_weight[player * 64 + s]
let owned = sectors[s].owner == player
let heal_ok = g.force < 8 and g.heal >= -3
let kind = 0
if hostile_owner(player, s):
    kind = 1
if prev == ACTION_NONE or prev == ACTION_CONTROL or prev == ACTION_HEAL:
    if heal_ok:
        plan(idx, ACTION_HEAL, 0, 0)
    else if previous_action_count(player, s, ACTION_CHAOS) == 0:
        plan(idx, ACTION_CHAOS, 0, 0)
    else:
        plan(idx, ACTION_MOVE, select_sector(player, 2, idx), 0)
    aux_records[idx].focus = -1
else if prev == ACTION_ATTACK or prev == ACTION_HIDE or prev == ACTION_MOVE:
    if w == 10:
        let t = draw_once(player, idx, kind)
        if t != -1:
            plan(idx, ACTION_ATTACK, t / 81, t % 81)
            aux_records[idx].focus = s
        else:
            plan(idx, ACTION_NONE, 0, 0)
            aux_records[idx].focus = -1
            aux_records[idx].coverage_sector = -1
    else if owned and previous_action_count(player, s, ACTION_CHAOS) == 0:
        plan(idx, ACTION_CHAOS, 0, 0)
    else if owned:
        plan(idx, ACTION_MOVE, select_sector(player, 2, idx), 0)
    else if prev == ACTION_MOVE and r.older_action == ACTION_MOVE and solo_control_ok(player, idx, s):
        plan(idx, ACTION_CONTROL, 0, 0)
    else:
        plan(idx, ACTION_MOVE, select_sector(player, 2, idx), 0)
else if prev == ACTION_CHAOS or prev == ACTION_EQUIP:
    if w == 10:
        let t = draw_target(player, idx, kind, 5)
        plan(idx, ACTION_ATTACK, t / 81, t % 81)
        aux_records[idx].focus = s
    if r.planned_action != ACTION_ATTACK and danger_near(player, idx):
        let wp = weapon_upgrade(player, idx)
        let ar = armor_upgrade(player, idx)
        if wp != -1 and r.weapon_cooldown <= 0:
            plan(idx, ACTION_EQUIP, wp, 0)
            r.weapon_cooldown = item_definitions[wp].cost * 3
        else if ar != -1 and r.armor_cooldown <= 0:
            plan(idx, ACTION_EQUIP, ar, 0)
            r.armor_cooldown = item_definitions[ar].cost * 3
    if r.planned_action != ACTION_EQUIP and r.planned_action != ACTION_ATTACK:
        if owned and previous_action_count(player, s, ACTION_CHAOS) < 2:
            plan(idx, ACTION_CHAOS, 0, 0)
        else:
            plan(idx, ACTION_MOVE, select_sector(player, 2, idx), 0)
# after Bribe, Give, Influence, Research, Sell, Snitch or Terminate the
# planned action stays None
```

## Outputs

No return value. Writes the gang's planned action and targets through `plan`:
Attack targets the drawn gang's player and roster slot, Move the sector
`select_sector` returns, Equip the item. An Equip sets the matching cooldown
to three times the item's cost. An Attack stores the gang's sector as its
`focus`; other branches may set the gang's two auxiliary values to -1. Never
changes the family. Draws one `roll` for a single target draw and up to five
for the five-draw loop, plus the draws inside `select_sector`.

## Edge cases

After Chaos or Equip at weight 10, the gang attacks the last target drawn even
when all five strength tests failed. After Chaos or Equip an owned sector
takes Chaos while at most one gang of the player raised Chaos there last turn,
while the other branches need none. The count includes the gang itself. The
strength test can be made on a different gang from the one attacked
(BUG-AI-003). The target pool follows the player's `attitude` toward the
owner query's value, with the out-of-row reads of RULE-AI-004 for a neutral
sector or one under police presence.

No cell of the family table writes 4 (RULE-AI-002), a blank cell writes 99,
and no handler or planning step writes 4, so the handler runs only for a
planning record loaded with family 4.

## What the sources say

SRC-MANUAL-GOG does not describe the computer players' strategies.

## Differences between builds

None known.

## Open questions

None.
