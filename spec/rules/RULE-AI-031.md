---
id: RULE-AI-031
title: Family-13 and family-14 computer gangs move to the Big Man or Siege objectives, fight for them on alternate turns and hold them
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-039, FND-AI-027, FND-AI-033, FND-AI-013, FND-EXE-004, FND-OBJECTIVE-003, FND-AI-055, FND-AI-062]
conflicting: []
split_with: []
related: [RULE-AI-004, RULE-AI-005, RULE-AI-006, RULE-AI-022, RULE-RNG-002, FMT-STATE-001, FMT-STATE-002]
---

## Summary

Families 13 and 14 play for the objective sectors: the four centre sectors in
scenario 8 (Big Man) and the six headquarters sectors in scenario 6 (Siege). A
gang off an objective moves toward one. On an objective its player does not
own, it fights the owner's visible gangs on turns when the number of turns
remaining is even and otherwise takes the sector by Control. On an owned
objective it fights visible gangs, heals, buys equipment, or influences a site
with Support. A family-14 gang that took its sector by Control
last turn and is hurt heals and becomes family 13.

## When it runs

From RULE-AI-002, for a gang whose family is 13 or 14.

## Parameters

`player`: the computer player. `slot`: the gang's roster slot.

## Inputs

The gang's record in `gangs` (`sector`, `force`, `heal`) and in
`planning_records` (`family`, `previous_action`, `weapon_cooldown`,
`armor_cooldown`), `aux_records`, `sector_weight`, `sectors` (`owner`, `sites`),
`item_definitions` (`cost`), `cash`, `scenario`, `rng_state` through `roll`,
and what the functions it calls read.

## Procedure

```text
# Whether sector s is one of the scenario's objective sectors (selector 0x1F)
define on_objective(s):
    let list = []
    if scenario == 8:
        list = [27, 28, 35, 36]
    else if scenario == 6:
        list = [9, 12, 30, 33, 51, 54]
    for each c in list:
        if c == s:
            return true
    return false

# Up to five draws from the pool of kind, then Attack at Force 5 or more,
# otherwise Heal when the Heal test passes, otherwise no write
define fight_or_heal(player, idx, kind):
    let g = gangs[idx]
    let t = -1
    if count(visible_opponents(player, g.sector, kind)) > 0:
        t = draw_target(player, idx, kind, 5)
    if t != -1 and g.force >= 5:
        plan(idx, ACTION_ATTACK, t / 81, t % 81)
        aux_records[idx].focus = g.sector
    else if g.force < 10 and g.heal >= -3:
        plan(idx, ACTION_HEAL, 0, 0)
        aux_records[idx].focus = -1
    return

# The Support scan, from the starting threshold best
define objective_site(s, best):
    let choice = -1
    for k in 0..3:
        let v = site_definitions[sectors[s].sites[k].definition].support
        if site_unfinished(s, k) and v > best:
            best = v
            choice = k
    return choice

let idx = player * 81 + slot
let g = gangs[idx]
let r = planning_records[idx]
let s = g.sector
let fam = r.family
let w = sector_weight[player * 64 + s]
let heal_ok = g.force < 10 and g.heal >= -3
if on_objective(s) and owner_query(s) != player:
    if turns_remaining() % 2 == 0 and w > 0:
        let kind = 3
        if w == 10 and hostile_owner(player, s):
            kind = 1
        fight_or_heal(player, idx, kind)
    else:
        plan(idx, ACTION_CONTROL, 0, 0)
        aux_records[idx].focus = -1
else if on_objective(s):
    if w > 0:
        let kind = 0
        if w == 10 and hostile_owner(player, s):
            kind = 1
        fight_or_heal(player, idx, kind)
    else if heal_ok:
        plan(idx, ACTION_HEAL, 0, 0)
        aux_records[idx].focus = -1
    else:
        let wp = weapon_upgrade(player, idx)
        let ar = armor_upgrade(player, idx)
        let mi = misc_control_upgrade(player, idx)
        let not_after_attack = r.previous_action != ACTION_ATTACK
        if wp != -1 and r.weapon_cooldown <= 0 and not_after_attack:
            plan(idx, ACTION_EQUIP, wp, 0)
            r.weapon_cooldown = 2
            aux_records[idx].focus = -1
        else if ar != -1 and r.armor_cooldown <= 0 and not_after_attack:
            plan(idx, ACTION_EQUIP, ar, 0)
            r.armor_cooldown = 2
            aux_records[idx].focus = -1
        else if mi != -1 and item_definitions[mi].cost <= cash[player]:
            plan(idx, ACTION_EQUIP, mi, 0)
            aux_records[idx].focus = -1
        else:
            # the handler never sets the starting threshold (BUG-AI-006)
            let site = objective_site(s, unset_stack_value)
            if site != -1:
                plan(idx, ACTION_INFLUENCE, site, 0)
                aux_records[idx].focus = s
            else:
                plan(idx, ACTION_NONE, 0, 0)
                aux_records[idx].focus = -1
if not on_objective(s) and r.planned_action != ACTION_EQUIP:
    let dest = r.planned_target
    if scenario == 8:
        dest = select_sector(player, 12 + (fam - 13) * 2, idx)
    if scenario == 6:
        dest = select_sector(player, 13 + (fam - 13) * 2, idx)
    plan(idx, ACTION_MOVE, dest, 0)
    aux_records[idx].focus = -1
else if fam == 14 and r.previous_action == ACTION_CONTROL and heal_ok:
    plan(idx, ACTION_HEAL, 0, 0)
    aux_records[idx].focus = -1
    r.family = 13
```

## Outputs

`on_objective` returns true or false. `fight_or_hold` and the handler return
nothing; they write the gang's planned action and targets through `plan`
(Attack targets the drawn gang's player and roster slot, Move the sector
`select_sector` returns, Equip the item, Influence the site slot). An Attack
or Influence stores the current sector in the first auxiliary value, and the
other writes store -1. A weapon or armor Equip sets the matching cooldown to 2.
A family-14 gang may become family 13. Draws up to five `roll`s on an
objective, plus the draws inside `select_sector`.

## Edge cases

On a contested objective the gang fights only when the number of turns
remaining is even, so it alternates between fighting and Control. A gang with
Force below 5, or one whose draws found no gang, never attacks: it heals when
Force is below 10 and effective Heal is at least -3, and otherwise the handler
writes nothing, so the planned action stays None (FND-AI-062). A neutral
objective has no owner's gangs to draw, so the gang heals or does nothing on
even turns. After the draws the last drawn gang is attacked even when every
strength test failed, and the strength test can be made on a different gang
from the one attacked (BUG-AI-003). In a scenario other than 6 and 8 no sector
is an objective, and the gang writes Move with the planned target left from the
start of the turn, sector 0.

## What the sources say

SRC-MANUAL-GOG, numbered page 14, lists Eliminate and Big Man among the
scenarios with objectives; it does not describe how computer players pursue
them.

## Differences between builds

None known.

## Open questions

- The value the Support scan starts from is left on the stack by earlier calls
  (BUG-AI-006); a run of the original is needed to know it.
- When the handler writes nothing, the planning record holds None from the
  start of the turn (RULE-AI-001); whether the gang record's action from the
  previous turn is cleared elsewhere before the turn resolves is not recorded.
- The Support field read through selector `0xC` is taken from FND-AI-039.
