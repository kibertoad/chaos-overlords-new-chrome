---
id: RULE-AI-023
title: Family-4 computer gangs hide in owned land, probe weak enemies and move through sector selector mode 2
status: disputed
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-031, FND-AI-033, FND-AI-021, FND-AI-002]
conflicting: [FND-AI-019]
split_with: []
related: [RULE-AI-004, RULE-AI-005, RULE-AI-006, RULE-RNG-002, FMT-STATE-001, FMT-STATE-002]
---

## Summary

Family 4 is a cautious family. A family-4 gang heals when hurt, hides in land
its player owns, tries to attack a weak hostile human gang it can see, and
otherwise moves through sector selector mode 2. It takes a sector by Control
only after two moves in a row. No cell of the family table assigns family 4,
so only a gang whose family byte already holds 4 runs this.

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
let hides = previous_action_count(player, s, ACTION_HIDE)
let kind = 0
if hostile_owner(player, s):
    kind = 1
if prev == ACTION_NONE or prev == ACTION_CONTROL or prev == ACTION_HEAL:
    if g.force < 8 and g.heal >= -3:
        plan(idx, ACTION_HEAL, 0, 0)
    else if hides == 0:
        plan(idx, ACTION_HIDE, 0, 0)
    else:
        plan(idx, ACTION_MOVE, select_sector(player, 2, idx), 0)
else if prev == ACTION_ATTACK or prev == ACTION_SNITCH or prev == ACTION_MOVE:
    if w == 10:
        let t = draw_once(player, idx, kind)
        if t != -1:
            plan(idx, ACTION_ATTACK, t / 81, t % 81)
        else:
            plan(idx, ACTION_NONE, 0, 0)
            aux_records[idx].focus = -1
            aux_records[idx].coverage_sector = -1
    else if owned and hides == 0:
        plan(idx, ACTION_HIDE, 0, 0)
    else if owned:
        plan(idx, ACTION_MOVE, select_sector(player, 2, idx), 0)
    else if prev == ACTION_MOVE and r.older_action == ACTION_MOVE and solo_control_ok(player, idx, s):
        plan(idx, ACTION_CONTROL, 0, 0)
    else:
        plan(idx, ACTION_MOVE, select_sector(player, 2, idx), 0)
else if prev == ACTION_HIDE or prev == ACTION_EQUIP:
    let done = false
    if w == 10:
        let t = draw_target(player, idx, kind, 5)
        plan(idx, ACTION_ATTACK, t / 81, t % 81)
        done = true
    if not done and danger_near(player, idx):
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
        if owned and hides < 2:
            plan(idx, ACTION_HIDE, 0, 0)
        else:
            plan(idx, ACTION_MOVE, select_sector(player, 2, idx), 0)
# the other previous actions leave the planned action None
```

## Outputs

No return value. Writes the gang's planned action and targets through `plan`:
Attack targets the drawn gang's player and roster slot, Move the sector
`select_sector` returns, Equip the item. An Equip sets the matching cooldown
to three times the item's cost. May set the gang's two auxiliary values to -1.
Never changes the family. Draws one `roll` for a single target draw and up to
five for the five-draw loop, plus the draws inside `select_sector`.

## Edge cases

After Hide or Equip at weight 10, the gang attacks the last target drawn even
when all five strength tests failed. After Hide or Equip an owned sector keeps
a second hiding gang (count below 2), while the other branches keep only one
(count 0). The strength test can be made on a different gang from the one
attacked (BUG-AI-003).

## What the sources say

SRC-MANUAL-GOG does not describe the computer players' strategies.

## Differences between builds

None known.

## Open questions

- Selector `0x5B` counts previous Hide in this handler's description
  (FND-AI-031) and previous Chaos in FND-AI-019; the procedure counts Hide.
  This is why the rule is disputed.
- How a gang first gets family 4 is not recorded; no cell of the family table
  assigns it and no handler writes it.
- The target pool for the draws is taken from the family-3 description
  (FND-AI-033).
- Whether the count read in the previous-Hide branch is taken before or after
  this turn's plans is not recorded; the procedure reads previous actions,
  which do not change during planning.
