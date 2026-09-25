---
id: RULE-AI-020
title: Family-1 computer gangs heal, raise Chaos, snitch, take sectors or wander, by previous action, cash and Mentality
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-020, FND-AI-021, FND-AI-004, FND-AI-028]
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
computers depends on the Mentality. When nothing applies it wanders through
sector selector mode 5.

## When it runs

From RULE-AI-002, for a gang whose family is 1.

## Parameters

`player`: the computer player. `slot`: the gang's roster slot.

## Inputs

The gang's record in `gangs` and in `planning_records` (`previous_action`,
`older_action`, `weapon_cooldown`, `armor_cooldown`), `sectors` (`owner`,
`tolerance`), `cash`, `mentality`, `controller`, `item_definitions` (`cost`),
and what the functions it calls read.

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
        let o = sectors[s].owner
        let crime = false
        if o >= 0 and is_human(o):
            crime = cash[player] >= 50 and mentality >= 1
        else:
            crime = o != player and o > 0 and cash[player] >= 50 and mentality == 0
        if crime and sectors[s].tolerance <= 3:
            plan(idx, ACTION_CHAOS, 0, 0)
        else if crime:
            plan(idx, ACTION_SNITCH, 0, 0)
        else:
            plan(idx, ACTION_MOVE, select_sector(player, 5, idx), 0)
# the other previous actions: see Open questions
```

## Outputs

No return value. Writes the gang's planned action and targets through `plan`
(Move targets the sector `select_sector` returns; Equip targets the item). An
Equip sets the matching cooldown to three times the item's cost. Draws from
`rng` only inside `select_sector`.

## Edge cases

No recorded family-1 branch heals a gang at Force 9. After None or Chaos the
Heal gate is Force below 8, and after Heal it is Force below 9. A computer
player's gang never commits a crime in a sector of player 0 at Goon, because
the owner test is "greater than 0" (BUG-AI-004). Cash of exactly 50 passes the
crime gate. In a neutral sector the crime gate fails (the owner is -1), so the
gang moves.

## What the sources say

SRC-MANUAL-GOG, numbered pages 14 and 15, orders the Mentality settings by
difficulty; it does not describe what a computer gang does.

## Differences between builds

None known.

## Open questions

- The branches for previous actions Attack, Bribe, Give, Hide, Influence,
  Move, Research, Sell and Terminate are not recorded; the procedure writes
  nothing for them, so the planned action stays None.
- A branch that keeps Snitch only while cash is strictly above 50, and
  otherwise writes Move, is recorded without the previous action that leads to
  it (FND-AI-020).
- A pair of Mentality gates includes an exact-Crime-Lord (Mentality 2) test
  whose branch is not recorded.
- How selector `0x35` treats a neutral owner (-1) is not recorded; the
  procedure treats a neutral sector as not human-owned.
- Whether the weapon choice is taken when its cooldown blocks it, so that
  armor is not tried, is not recorded; the procedure tries armor when the
  weapon is missing or its cooldown has not run out.
- The Tolerance byte read is taken to be `tolerance` at sector record +5.
