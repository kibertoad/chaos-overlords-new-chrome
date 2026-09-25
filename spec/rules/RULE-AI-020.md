---
id: RULE-AI-020
title: Family-1 computer gangs heal, raise Chaos, snitch, take sectors or wander, by previous action, cash and Mentality
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-020, FND-AI-021, FND-AI-004, FND-AI-028, FND-EXE-004, FND-AI-042, FND-AI-057]
conflicting: []
split_with: []
related: [RULE-AI-004, RULE-AI-005, RULE-AI-006, FMT-STATE-001, FMT-STATE-002]
---

## Summary

A family-1 gang carries on from what it did last turn. An injured gang heals
unless police are in its sector. After Control, Equip or Snitch it first
upgrades its equipment when enemies are near; otherwise, with at least 50 cash,
it commits a crime in a sector owned by someone else: Chaos where Tolerance is
3 or less, Snitch where it is higher. Whether it does so against humans or
computers depends on the Mentality. After Attack, Hide or Move it attacks a
weaker gang when a hostile human's gang is in sight, and otherwise heals, takes
the sector, snitches or moves on. When nothing applies it wanders through
sector selector mode 5. In the last three turns of a Greed match every gang
terminates.

## When it runs

From RULE-AI-002, for a gang whose family is 1.

## Parameters

`player`: the computer player. `slot`: the gang's roster slot.

## Inputs

The gang's record in `gangs` and in `planning_records` (`previous_action`,
`older_action`, `weapon_cooldown`, `armor_cooldown`), `aux_records`, `sectors`
(`owner`, `tolerance`), `sector_weight`, `cash`, `mentality`, `scenario`,
`item_definitions` (`cost`), and what the functions it calls read.

## Procedure

```text
let idx = player * 81 + slot
let g = gangs[idx]
let r = planning_records[idx]
let s = g.sector
let prev = r.previous_action
if prev == ACTION_NONE or prev == ACTION_CHAOS:
    if g.force < 8 and g.heal >= -3:
        if not crackdown_in_force(s):
            plan(idx, ACTION_HEAL, 0, 0)
        else:
            plan(idx, ACTION_MOVE, select_sector(player, 5, idx), 0)
    else if r.older_action == ACTION_SNITCH:
        plan(idx, ACTION_CHAOS, 0, 0)
    else:
        plan(idx, ACTION_MOVE, select_sector(player, 5, idx), 0)
else if prev == ACTION_HEAL:
    if g.force < 9 and g.heal >= -3:
        plan(idx, ACTION_HEAL, 0, 0)
    else if solo_control_ok(player, idx, s):
        plan(idx, ACTION_CONTROL, 0, 0)
    else:
        plan(idx, ACTION_MOVE, select_sector(player, 5, idx), 0)
else if prev == ACTION_CONTROL or prev == ACTION_EQUIP or prev == ACTION_SNITCH:
    let equipped = false
    if danger_near(player, idx):
        let w = weapon_upgrade(player, idx)
        let a = armor_upgrade(player, idx)
        if w != -1 and r.weapon_cooldown <= 0:
            plan(idx, ACTION_EQUIP, w, 0)
            r.weapon_cooldown = item_definitions[w].cost * 3
            equipped = true
        else if a != -1 and r.armor_cooldown <= 0:
            plan(idx, ACTION_EQUIP, a, 0)
            r.armor_cooldown = item_definitions[a].cost * 3
            equipped = true
    if not equipped:
        let q = owner_query(s)
        let crime = false
        if owner_is_human(s) and cash[player] >= 50 and mentality >= 1:
            crime = true
        else if q != player and q > 0 and cash[player] >= 50 and mentality == 0:
            crime = true
        if crime and sectors[s].tolerance <= 3:
            plan(idx, ACTION_CHAOS, 0, 0)
        else if crime:
            plan(idx, ACTION_SNITCH, 0, 0)
        else:
            plan(idx, ACTION_MOVE, select_sector(player, 5, idx), 0)
else if prev == ACTION_ATTACK or prev == ACTION_HIDE or prev == ACTION_MOVE:
    let w = sector_weight[player * 64 + s]
    let fallback = false
    if w == 10:
        let kind = 0
        if hostile_owner(player, s):
            kind = 1
        let t = draw_once(player, idx, kind)
        if t != -1:
            plan(idx, ACTION_ATTACK, t / 81, t % 81)
            aux_records[idx].focus = s
        else:
            fallback = true
    else if owner_query(s) == player:
        plan(idx, ACTION_MOVE, select_sector(player, 5, idx), 0)
    else:
        fallback = true
    if fallback:
        let hostile_gate = hostile_owner(player, s) and mentality >= 1
        let snitch_ok = owner_is_human(s) and (hostile_gate or mentality == 2)
        if g.force < 9 and g.heal >= -3:
            plan(idx, ACTION_HEAL, 0, 0)
        else if solo_control_ok(player, idx, s):
            plan(idx, ACTION_CONTROL, 0, 0)
        else if snitch_ok and cash[player] > 50:
            plan(idx, ACTION_SNITCH, 0, 0)
        else:
            plan(idx, ACTION_MOVE, select_sector(player, 5, idx), 0)
# previous Bribe, Give, Influence, Research, Sell and Terminate: no write
# every action written above except Attack clears the first auxiliary value
if r.planned_action != ACTION_NONE and r.planned_action != ACTION_ATTACK:
    aux_records[idx].focus = -1
if scenario == 0 and turns_remaining() < 4:
    plan(idx, ACTION_TERMINATE, 0, 0)
    r.needs_family = 1
```

## Outputs

No return value. Writes the gang's planned action and targets through `plan`
(Move targets the sector `select_sector` returns; Equip targets the item). An
Equip sets the matching cooldown to three times the item's cost. An Attack
stores the current sector in the first auxiliary value, and every other action
the handler writes stores -1. The scenario-0 Terminate also sets
`needs_family`. Draws from `rng` only inside `select_sector` and
`draw_once`.

## Edge cases

No recorded family-1 branch heals a gang at Force 9. After None or Chaos the
Heal gate is Force below 8, and after Heal it is Force below 9. A computer
player's gang never commits a crime in a sector of player 0 at Goon, because
the owner test is "greater than 0" (BUG-AI-004). Cash of exactly 50 passes the
crime gate. A human owner that fails the first crime test still gets the Goon
test, so at Goon a gang commits crimes in the sectors of humans in slots 1 to 5.
Under police presence the owner query is -2 and the Goon test fails. A neutral
sector counts as human-owned when player 5 has 0 or 3 casualties
(`owner_is_human`), so at Mentality 1 or 2 a gang can commit a crime or snitch
in a neutral sector. After Attack, Hide or Move, Snitch needs cash strictly
above 50.

## What the sources say

SRC-MANUAL-GOG, numbered pages 14 and 15, orders the Mentality settings by
difficulty; it does not describe what a computer gang does.

## Differences between builds

None known.

## Open questions

- Whether the weapon choice is taken when its cooldown blocks it, so that
  armor is not tried, is not recorded; the procedure tries armor when the
  weapon is missing or its cooldown has not run out.
- The Tolerance byte read is taken to be `tolerance` at sector record +5.
