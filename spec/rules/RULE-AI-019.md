---
id: RULE-AI-019
title: Family-0 computer gangs heal, hide, probe weak enemies or wander, by previous action, and turn aggressive after two moves
status: disputed
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-030, FND-AI-033, FND-AI-021, FND-AI-015, FND-AI-028]
conflicting: [FND-AI-019]
split_with: []
related: [RULE-AI-004, RULE-AI-005, RULE-AI-006, RULE-RNG-002, FMT-STATE-001, FMT-STATE-002]
---

## Summary

Family 0 is the default family. A family-0 gang carries on from its previous
action: an injured gang heals, a gang that sees a hostile human gang tries to
attack one it can beat, a gang alone in a sector it could take tries Control,
and otherwise it hides or wanders through sector selector mode 5. After two
moves in a row it joins family 11 in scenario 7 and family 2 elsewhere.

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
    else if previous_action_count(player, s, ACTION_HIDE) == 0:
        plan(idx, ACTION_HIDE, 0, 0)
    else:
        plan(idx, ACTION_MOVE, select_sector(player, 5, idx), 0)
else if prev == ACTION_ATTACK:
    if w != 10:
        plan(idx, ACTION_MOVE, select_sector(player, 5, idx), 0)
    else:
        let t = draw_once(player, idx, kind)
        if t != -1:
            plan(idx, ACTION_ATTACK, t / 81, t % 81)
        else if solo_control_ok(player, idx, s):
            plan(idx, ACTION_CONTROL, 0, 0)
        else:
            plan(idx, ACTION_MOVE, select_sector(player, 5, idx), 0)
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
        if sectors[s].owner == player and heal_ok:
            plan(idx, ACTION_HEAL, 0, 0)
        else if sectors[s].owner == player:
            plan(idx, ACTION_HIDE, 0, 0)
        else:
            plan(idx, ACTION_MOVE, select_sector(player, 5, idx), 0)
else if prev == ACTION_CONTROL:
    if sectors[s].owner == player:
        plan(idx, ACTION_HIDE, 0, 0)
    else:
        plan(idx, ACTION_MOVE, select_sector(player, 5, idx), 0)
else if prev == ACTION_HEAL or prev == ACTION_SNITCH or prev == ACTION_MOVE:
    if heal_ok:
        plan(idx, ACTION_HEAL, 0, 0)
    else if w == 10:
        let t = draw_once(player, idx, kind)
        if t != -1:
            plan(idx, ACTION_ATTACK, t / 81, t % 81)
        else:
            plan(idx, ACTION_NONE, 0, 0)
            aux_records[idx].focus = -1
            aux_records[idx].coverage_sector = -1
    else if solo_control_ok(player, idx, s):
        plan(idx, ACTION_CONTROL, 0, 0)
    else if previous_action_count(player, s, ACTION_HIDE) == 0:
        plan(idx, ACTION_HIDE, 0, 0)
    else:
        plan(idx, ACTION_MOVE, select_sector(player, 5, idx), 0)
else if prev == ACTION_RESEARCH:
    plan(idx, ACTION_MOVE, select_sector(player, 5, idx), 0)
# the other previous actions leave the planned action None
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
three times the item's cost. May set the gang's two auxiliary values to -1 and
its family to 11 or 2. Draws one `roll` for a single target draw and up to five
for the five-draw loop, in the order the procedure makes them, plus the draws
inside `select_sector`.

## Edge cases

After Hide or Equip at weight 10, the gang attacks the last target drawn even
when all five strength tests failed. The strength test can be made on a
different gang from the one attacked (BUG-AI-003). After Heal, Snitch or Move a
failed single draw leaves the gang doing nothing this turn. The family change
tests the older action, so Move, then anything, then Move is enough.

## What the sources say

SRC-MANUAL-GOG does not describe the computer players' strategies.

## Differences between builds

None known.

## Open questions

- Selector `0x5B` counts previous Hide in this handler's description
  (FND-AI-030) and previous Chaos in FND-AI-019. The procedure counts Hide; if
  the selector counts Chaos, both Hide-or-Move choices change. This is why the
  rule is disputed.
- The target pool for the draws is taken from the family-3 description
  (FND-AI-033): the human-only list in a sector owned by a hostile player, the
  full list otherwise. FND-AI-030 names the human pool for the previous-Attack
  draw without the owner condition.
- The choice between Heal and Hide in an owned sector after Hide or Equip is not
  written out; the procedure uses the usual Heal gate.
- Whether the equipment step after Hide or Equip is gated by selector `0x6C`
  (`danger_near`) alone, or by it and the weight, is inferred.
- Whether the final family change reads the new action from the planning
  record or from a local value is not recorded; the effect is the same.
