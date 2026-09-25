---
id: RULE-AI-021
title: Family-2 computer gangs equip, heal, attack visible hostile gangs and take weak or hostile sectors
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-032, FND-AI-033, FND-AI-018, FND-AI-015, FND-AI-028, FND-EXE-004, FND-AI-042, FND-AI-058, FND-AI-057]
conflicting: []
split_with: []
related: [RULE-AI-003, RULE-AI-004, RULE-AI-005, RULE-AI-006, RULE-RNG-002, FMT-STATE-001, FMT-STATE-002]
---

## Summary

Family 2 is the aggressive family. A family-2 gang first buys better armor or
a better weapon, then heals if hurt and safe, then leaves land its player owns
through sector selector mode 6. In land it does not own it attacks a visible
hostile gang, or takes the sector by Control. A gang standing in the sector of
an opponent its player already out-fights takes it by Control. In Greed,
during the last three turns, it terminates instead.

## When it runs

From RULE-AI-002, for a gang whose family is 2.

## Parameters

`player`: the computer player. `slot`: the gang's roster slot.

## Inputs

The gang's record in `gangs` (`sector`, `force`, `heal`) and in
`planning_records` (`previous_action`, `weapon_cooldown`, `armor_cooldown`),
`sector_weight`, `sectors` (`owner`), `attitude`, `combat_advantage`,
`scenario`, `item_definitions` (`cost`), `rng_state` through `roll`, and what
the functions it calls read.

## Procedure

```text
let idx = player * 81 + slot
let g = gangs[idx]
let r = planning_records[idx]
let s = g.sector
let prev = r.previous_action
let w = sector_weight[player * 64 + s]
let done = false
if prev != ACTION_ATTACK:
    let ar = armor_upgrade(player, idx)
    let wp = weapon_upgrade(player, idx)
    if ar != -1 and r.armor_cooldown <= 0:
        plan(idx, ACTION_EQUIP, ar, 0)
        r.armor_cooldown = item_definitions[ar].cost * 3
        aux_records[idx].focus = -1
        done = true
    else if wp != -1 and r.weapon_cooldown <= 0:
        plan(idx, ACTION_EQUIP, wp, 0)
        r.weapon_cooldown = item_definitions[wp].cost * 3
        aux_records[idx].focus = -1
        done = true
if not done and g.force < 8 and g.heal >= -3 and w < 5:
    plan(idx, ACTION_HEAL, 0, 0)
    aux_records[idx].focus = -1
    done = true
if not done and owner_query(s) == player:
    plan(idx, ACTION_MOVE, select_sector(player, 6, idx), 0)
    aux_records[idx].focus = -1
    done = true
if not done and w > 0 and count(visible_opponents(player, s, 2)) > 0:
    let kind = 2
    if w == 10:
        kind = 1
    let t = draw_target(player, idx, kind, 5)
    plan(idx, ACTION_ATTACK, t / 81, t % 81)
    aux_records[idx].focus = s
    done = true
if not done:
    if prev == ACTION_CONTROL or scenario == 9 or not solo_control_ok(player, idx, s):
        plan(idx, ACTION_MOVE, select_sector(player, 6, idx), 0)
    else:
        plan(idx, ACTION_CONTROL, 0, 0)
    aux_records[idx].focus = -1
# two late gates that can replace any action chosen above; a cooldown stays
let q = owner_query(s)
let gate_1 = false
if hostile_owner(player, s) and count(visible_opponents(player, s, 1)) == 0:
    gate_1 = owner_is_human(s) and prev != ACTION_CONTROL
let gate_2 = false
if hostile_owner(player, s) and count(visible_opponents(player, s, 3)) == 0:
    gate_2 = combat_advantage[player * 6 + q] != 0
if gate_1 or gate_2:
    plan(idx, ACTION_CONTROL, 0, 0)
    aux_records[idx].focus = -1
if scenario == 0 and turns_remaining() < 4:
    plan(idx, ACTION_TERMINATE, 0, 0)
    r.needs_family = 1
```

## Outputs

No return value. Writes the gang's planned action and targets through `plan`,
and sets the gang's `focus` to its sector after an Attack and to -1 after any
other action. An Equip sets the matching cooldown to three times the item's
cost. Draws up to five `roll`s in the attack loop, plus the draws inside
`select_sector`.

## Edge cases

The attack loop attacks the last drawn target even when all five strength tests
fail, and the strength test can be made on a different gang from the one
attacked (BUG-AI-003). A gang whose previous action was Attack never equips. In
a sector the player owns, the late gates never fire, because both need an
owner the player is hostile to. The Greed override replaces every other choice,
including an Equip whose cooldown has already been set. The late gates also
replace an Equip or a Heal and leave the cooldown in place. Under police
presence the owner query is -2, so a gang in its own sector skips the mode-6
Move and goes on to the attack step. The attack step needs at least one
visible gang of a player the player is hostile to, even at weight 10, where
the draw then takes the human pool (FND-AI-058).

## What the sources say

SRC-MANUAL-GOG does not describe the computer players' strategies.

## Differences between builds

None known.

## Open questions

- The late gate reads `combat_advantage` at `0x0042085D`; that it is the value
  RULE-AI-003 sets is taken from FND-AI-018.
- Whether the Heal gate reads the same weight as the attack step (the cached
  weight of the current sector) is taken from FND-AI-032.
