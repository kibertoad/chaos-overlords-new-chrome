---
id: RULE-AI-022
title: Family-3 computer gangs influence the best Cash site in owned land, take sectors or move toward Cash
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-033, FND-AI-034, FND-AI-021, FND-AI-026, FND-AI-028, FND-EXE-004, FND-AI-042]
conflicting: []
split_with: []
related: [RULE-AI-004, RULE-AI-005, RULE-AI-006, RULE-RNG-002, FMT-STATE-001, FMT-STATE-002, FMT-STATE-004]
---

## Summary

Family 3 builds Cash. A family-3 gang heals when hurt, influences the site
with the most Cash that is not yet won over in a sector its player owns, takes
the sector by Control when it can, and otherwise moves through sector selector
mode 8 toward owned land with Cash still to gain. It keeps influencing the same
site until the site is won. A gang that sees a hostile human gang tries to
attack one it can beat. After three moves in a row it joins family 11 in
scenario 7 and family 2 elsewhere. Family 5 (RULE-AI-024) runs the same
procedure with Support in place of Cash.

## When it runs

From RULE-AI-002, for a gang whose family is 3.

## Parameters

`player`: the computer player. `slot`: the gang's roster slot.

## Inputs

The gang's record in `gangs` (`sector`, `force`, `heal`) and in
`planning_records` (`previous_action`, `previous_target`, `older_action`,
`weapon_cooldown`, `armor_cooldown`), `sector_weight`, `sectors` (`owner`,
`sites`), `site_definitions` (`cash`, `support`), `scenario`,
`item_definitions` (`cost`), `rng_state` through `roll`, and what the
functions it calls read.

## Procedure

```text
# The slot of the unfinished site in sector s with the first strictly greatest
# positive Cash (kind 0) or Support (kind 1), or -1
define best_site(s, kind):
    let best = 0
    let choice = -1
    for k in 0..3:
        let site = sectors[s].sites[k]
        let v = site_definitions[site.definition].cash
        if kind == 1:
            v = site_definitions[site.definition].support
        if site_unfinished(s, k) and v > best:
            best = v
            choice = k
    return choice

# The shared handler of families 3 (kind 0, mode 8) and 5 (kind 1, mode 7)
define site_builder(player, slot, kind):
    let mode = 8
    if kind == 1:
        mode = 7
    let idx = player * 81 + slot
    let g = gangs[idx]
    let r = planning_records[idx]
    let s = g.sector
    let prev = r.previous_action
    let heal_ok = g.force < 8 and g.heal >= -3
    let w = sector_weight[player * 64 + s]
    let owned = sectors[s].owner == player
    let site = -1
    if owned:
        site = best_site(s, kind)
    if prev == ACTION_NONE or prev == ACTION_CONTROL or prev == ACTION_EQUIP or prev == ACTION_HEAL:
        if heal_ok:
            plan(idx, ACTION_HEAL, 0, 0)
        else if site != -1:
            plan(idx, ACTION_INFLUENCE, site, 0)
        else if solo_control_ok(player, idx, s):
            plan(idx, ACTION_CONTROL, 0, 0)
        else:
            plan(idx, ACTION_MOVE, select_sector(player, mode, idx), 0)
    else if prev == ACTION_SNITCH:
        plan(idx, ACTION_MOVE, select_sector(player, mode, idx), 0)
    else if prev == ACTION_INFLUENCE:
        let done = false
        if danger_near(player, idx):
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
            if heal_ok:
                plan(idx, ACTION_HEAL, 0, 0)
            else if owned and site_unfinished(s, r.previous_target):
                plan(idx, ACTION_INFLUENCE, r.previous_target, 0)
            else if site != -1:
                plan(idx, ACTION_INFLUENCE, site, 0)
            else:
                plan(idx, ACTION_MOVE, select_sector(player, mode, idx), 0)
    else if prev == ACTION_ATTACK or prev == ACTION_HIDE or prev == ACTION_MOVE:
        if w != 10:
            if site != -1:
                plan(idx, ACTION_INFLUENCE, site, 0)
            else if solo_control_ok(player, idx, s):
                plan(idx, ACTION_CONTROL, 0, 0)
            else:
                plan(idx, ACTION_MOVE, select_sector(player, mode, idx), 0)
        else:
            let kind_pool = 0
            if hostile_owner(player, s):
                kind_pool = 1
            let t = draw_once(player, idx, kind_pool)
            if t != -1:
                plan(idx, ACTION_ATTACK, t / 81, t % 81)
            else:
                plan(idx, ACTION_NONE, 0, 0)
                aux_records[idx].focus = -1
                aux_records[idx].coverage_sector = -1
    # the other previous actions leave the planned action None
    if r.planned_action == ACTION_MOVE and prev == ACTION_MOVE and r.older_action == ACTION_MOVE:
        if scenario == 7:
            r.family = 11
        else:
            r.family = 2
    if scenario == 0 and turns_remaining() < 4:
        plan(idx, ACTION_TERMINATE, 0, 0)
        r.needs_family = 1
    return

site_builder(player, slot, 0)
```

## Outputs

No return value. Writes the gang's planned action and targets through `plan`:
Influence targets the site slot, Attack the drawn gang's player and roster
slot, Move the sector `select_sector` returns, Equip the item. An Equip sets
the matching cooldown to three times the item's cost. May set the gang's
auxiliary values to -1 and its family to 11 or 2. Draws one `roll` for the
single target draw, plus the draws inside `select_sector`.

## Edge cases

A site whose Cash is 0 or negative is never chosen. On equal Cash the site in
the lower slot wins. After Attack, Hide or Move without a visible hostile human
gang, the Heal gate is skipped, so a badly hurt gang can go on influencing. A
failed draw leaves the gang doing nothing this turn. The strength test can be
made on a different gang from the one attacked (BUG-AI-003).

## What the sources say

SRC-MANUAL-GOG does not describe the computer players' strategies.

## Differences between builds

None known.

## Open questions

- The cases without an action body (so that the gang plans None) are not
  written out; the procedure leaves Bribe, Chaos, Give, Research, Sell and
  Terminate without a body.
- "Three Moves in a row" is read from the new, previous and older actions.
- The kept-site test (selector `0x41`) reads the first target byte of the
  previous action as a site slot; the procedure assumes the ownership test is
  of the current sector.
- Whether a successful Attack also sets the gang's `focus` is not recorded for
  this family.
