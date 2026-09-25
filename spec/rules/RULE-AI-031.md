---
id: RULE-AI-031
title: Family-13 and family-14 computer gangs move to the Big Man or Siege objectives, fight for them on alternate turns and hold them
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-039, FND-AI-027, FND-AI-033, FND-AI-013, FND-EXE-004, FND-OBJECTIVE-003, FND-AI-055]
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
objective it heals, fights visible gangs, buys equipment, or influences the
site with the most Support. A family-14 gang that took its sector by Control
last turn and is hurt heals and becomes family 13.

## When it runs

From RULE-AI-002, for a gang whose family is 13 or 14.

## Parameters

`player`: the computer player. `slot`: the gang's roster slot.

## Inputs

The gang's record in `gangs` (`sector`, `force`, `heal`) and in
`planning_records` (`family`, `previous_action`, `weapon_cooldown`,
`armor_cooldown`), `sector_weight`, `sectors` (`owner`, `sites`),
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

# Attack the gang the draws give, or Heal, or Control
define fight_or_hold(player, idx, kind, tries):
    let g = gangs[idx]
    let t = -1
    if count(visible_opponents(player, g.sector, kind)) > 0:
        t = draw_target(player, idx, kind, tries)
    if t != -1 and g.force >= 5:
        plan(idx, ACTION_ATTACK, t / 81, t % 81)
    else if g.force < 10 and g.heal >= -3:
        plan(idx, ACTION_HEAL, 0, 0)
    else:
        plan(idx, ACTION_CONTROL, 0, 0)
    return

let idx = player * 81 + slot
let g = gangs[idx]
let r = planning_records[idx]
let s = g.sector
let fam = r.family
let w = sector_weight[player * 64 + s]
let heal_ok = g.force < 10 and g.heal >= -3
let mode = 12
if scenario == 6:
    mode = 13
if fam == 14:
    mode = mode + 2
if not on_objective(s):
    plan(idx, ACTION_MOVE, select_sector(player, mode, idx), 0)
else if sectors[s].owner != player:
    if turns_remaining() % 2 == 0 and w != 0:
        let kind = 3
        if w == 10 and hostile_human_owner(player, s):
            kind = 1
        fight_or_hold(player, idx, kind, 3)
    else:
        plan(idx, ACTION_CONTROL, 0, 0)
else if w == 0 and heal_ok:
    plan(idx, ACTION_HEAL, 0, 0)
else if w != 0:
    fight_or_hold(player, idx, 0, 5)
else:
    let wp = weapon_upgrade(player, idx)
    let ar = armor_upgrade(player, idx)
    let mi = misc_control_upgrade(player, idx)
    let site = best_site(s, 1)
    if wp != -1 and r.weapon_cooldown <= 0 and r.previous_action != ACTION_ATTACK:
        plan(idx, ACTION_EQUIP, wp, 0)
        r.weapon_cooldown = 2
    else if ar != -1 and r.armor_cooldown <= 0 and r.previous_action != ACTION_ATTACK:
        plan(idx, ACTION_EQUIP, ar, 0)
        r.armor_cooldown = 2
    else if mi != -1 and item_definitions[mi].cost <= cash[player]:
        plan(idx, ACTION_EQUIP, mi, 0)
    else if site != -1:
        plan(idx, ACTION_INFLUENCE, site, 0)
    else:
        plan(idx, ACTION_NONE, 0, 0)
if fam == 14 and on_objective(s) and r.previous_action == ACTION_CONTROL and heal_ok:
    plan(idx, ACTION_HEAL, 0, 0)
    r.family = 13
```

## Outputs

`on_objective` returns true or false. `fight_or_hold` and the handler return
nothing; they write the gang's planned action and targets through `plan`
(Attack targets the drawn gang's player and roster slot, Move the sector
`select_sector` returns, Equip the item, Influence the site slot). A weapon or
armor Equip sets the matching cooldown to 2. A family-14 gang may become
family 13. Draws up to three `roll`s on a contested objective and up to five on
an owned one, plus the draws inside `select_sector`.

## Edge cases

On a contested objective the gang fights only when the number of turns remaining
is even, so it alternates between fighting and Control. A gang with Force below
5 never attacks and heals or takes Control instead. After the draws the last
drawn gang is attacked even when every strength test failed, and the strength
test can be made on a different gang from the one attacked (BUG-AI-003). In a
scenario other than 6 and 8 no sector is an objective and the gang always
moves, through mode 12 or 14.

## What the sources say

SRC-MANUAL-GOG, numbered page 14, lists Eliminate and Big Man among the
scenarios with objectives; it does not describe how computer players pursue
them.

## Differences between builds

None known.

## Open questions

- The owned-objective Heal branch was located only by decompiler positions
  (FND-AI-039).
- The contested pool is taken to be the visible gangs of the sector's owner
  (kind 3); it may be every visible opponent's gang.
- Whose Force the "Force 5 or more" test reads is not stated; the procedure
  reads the acting gang's.
- The Support-site scan is taken to be `best_site` of RULE-AI-022.
