---
id: RULE-AI-019
title: Family-0 computer gangs heal, raise Chaos, probe weak enemies or wander, by previous action, and turn aggressive after two moves
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-048, FND-AI-046, FND-AI-033, FND-AI-021, FND-AI-015, FND-AI-028, FND-AI-044, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-AI-004, RULE-AI-005, RULE-AI-006, RULE-RNG-002, FMT-STATE-001, FMT-STATE-002]
---

## Summary

Family 0 is the default family. A family-0 gang carries on from its previous
action: an injured gang heals, a gang that sees a hostile human gang tries to
attack one it can beat, a gang alone in a sector it could take tries Control,
and otherwise it raises Chaos where no gang of its player did so last turn, or
wanders through sector selector mode 5. After two moves in a row it joins
family 11 in scenario 7 and family 2 elsewhere.

## When it runs

From RULE-AI-002, for a gang whose family is 0.

## Parameters

`player`: the computer player. `slot`: the gang's roster slot.

## Inputs

The gang's record in `gangs` (`sector`, `force`, `heal`) and in
`planning_records` (`previous_action`, `older_action`, `weapon_cooldown`,
`armor_cooldown`), `sector_weight`, `sectors` (`owner`), `scenario`,
`item_definitions` (`cost`), `rng_state` through `roll`, and what the
functions it calls read.

## Procedure

```text
let idx = player * 81 + slot
let g = gangs[idx]
let r = planning_records[idx]
let s = g.sector
let prev = r.previous_action
let w = sector_weight[player * 64 + s]
let heal_ok = g.force < 8 and g.heal >= -3
let kind = 0
if hostile_owner(player, s):
    kind = 1
if prev == ACTION_NONE:
    if heal_ok:
        plan(idx, ACTION_HEAL, 0, 0)
    else if previous_action_count(player, s, ACTION_CHAOS) == 0:
        plan(idx, ACTION_CHAOS, 0, 0)
    else:
        plan(idx, ACTION_MOVE, select_sector(player, 5, idx), 0)
else if prev == ACTION_ATTACK:
    if w != 10:
        plan(idx, ACTION_MOVE, select_sector(player, 5, idx), 0)
    else:
        let t = draw_once(player, idx, kind)
        if t != -1:
            plan(idx, ACTION_ATTACK, t / 81, t % 81)
            aux_records[idx].focus = s
        else if solo_control_ok(player, idx, s):
            plan(idx, ACTION_CONTROL, 0, 0)
        else:
            plan(idx, ACTION_MOVE, select_sector(player, 5, idx), 0)
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
        if sectors[s].owner == player and heal_ok:
            plan(idx, ACTION_HEAL, 0, 0)
        else if sectors[s].owner == player:
            plan(idx, ACTION_CHAOS, 0, 0)
        else:
            # a weight of 10 has already produced an Attack above
            plan(idx, ACTION_MOVE, select_sector(player, 5, idx), 0)
else if prev == ACTION_CONTROL:
    if sectors[s].owner == player:
        plan(idx, ACTION_CHAOS, 0, 0)
    else:
        plan(idx, ACTION_MOVE, select_sector(player, 5, idx), 0)
else if prev == ACTION_HEAL or prev == ACTION_HIDE or prev == ACTION_MOVE:
    if heal_ok:
        plan(idx, ACTION_HEAL, 0, 0)
    else if w == 10:
        let t = draw_once(player, idx, kind)
        if t != -1:
            plan(idx, ACTION_ATTACK, t / 81, t % 81)
            aux_records[idx].focus = s
        else:
            plan(idx, ACTION_NONE, 0, 0)
            aux_records[idx].focus = -1
            aux_records[idx].coverage_sector = -1
    else if solo_control_ok(player, idx, s):
        plan(idx, ACTION_CONTROL, 0, 0)
    else if previous_action_count(player, s, ACTION_CHAOS) == 0:
        plan(idx, ACTION_CHAOS, 0, 0)
    else:
        plan(idx, ACTION_MOVE, select_sector(player, 5, idx), 0)
else if prev == ACTION_SNITCH:
    plan(idx, ACTION_MOVE, select_sector(player, 5, idx), 0)
# after Bribe, Give, Influence, Research or Sell the planned action stays None
if r.planned_action == ACTION_MOVE and r.older_action == ACTION_MOVE:
    if scenario == 7:
        r.family = 11
    else:
        r.family = 2
```

## Outputs

No return value. Writes the gang's planned action and targets through `plan`:
Attack targets the drawn gang's player and roster slot, Move the sector
`select_sector` returns, Equip the item. An Equip sets the matching cooldown to
three times the item's cost. An Attack stores the gang's sector as its `focus`;
most other branches set `focus` to -1, which the procedure leaves out where
nothing reads it before the next write. May set both auxiliary values to -1 and
the family to 11 or 2. Draws one `roll` for a single target draw and up to five
for the five-draw loop, in the order the procedure makes them, plus the draws
inside `select_sector`.

## Edge cases

After Chaos or Equip at weight 10, the gang attacks the last target drawn even
when all five strength tests failed. The strength test can be made on a
different gang from the one attacked (BUG-AI-003). After Heal, Hide or Move a
failed single draw leaves the gang doing nothing this turn. The Chaos count
includes the gang itself, so a gang that raised Chaos last turn moves on unless
it is injured or sees an enemy, and after previous Chaos in its own sector it
heals or raises Chaos again. After Bribe, Give, Influence, Research or Sell the
gang plans nothing. The family change tests the older action, so Move, then
anything, then Move is enough.

The target pool follows the player's `attitude` toward the value the owner
query returns (RULE-AI-004), so a neutral sector or one under police presence
selects the pool from a cell outside the player's row of `attitude`.

## What the sources say

SRC-MANUAL-GOG does not describe the computer players' strategies.

## Differences between builds

None known.

## Open questions

- Whether the final family change reads the new action from the planning
  record or from a local value is not recorded; the effect is the same.
