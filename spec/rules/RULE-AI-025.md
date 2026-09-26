---
id: RULE-AI-025
title: Family-6 computer gangs hunt sectors with visible hostile human gangs and fight there
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-029, FND-AI-013, FND-AI-015, FND-AI-033, FND-AI-028, FND-EXE-004, FND-AI-042, FND-AI-059]
conflicting: []
split_with: []
related: [RULE-AI-004, RULE-AI-005, RULE-AI-006, RULE-RNG-002, FMT-STATE-001, FMT-STATE-002]
---

## Summary

Family 6 is the hunter. Where it sees no other gang, a family-6 gang heads for
the first sector, in sector order, where a gang of a hostile human is visible
and no other hunter is already going. Where it sees gangs it tries an attack it
can win, then better equipment, and then attacks anyway after up to five more
draws. In Greed, during the last three turns, it terminates instead.

## When it runs

From RULE-AI-002, for a gang whose family is 6. `covered_by` also runs in the
hire planner (RULE-AI-010), which sends a new family-6 gang toward an
uncovered sector.

## Parameters

`player`: the computer player. `slot`: the gang's roster slot.

## Inputs

The gang's record in `gangs` (`sector`) and in `planning_records` (`family`,
`weapon_cooldown`, `armor_cooldown`), `aux_records` (`focus`,
`coverage_sector`), `sector_weight`, `sectors` (`owner`), `scenario`,
`item_definitions` (`cost`), `rng_state` through `roll`, and what the
functions it calls read.

## Procedure

```text
# An active family-6 gang of the player covering sector c, or -1 (selector 0x5F)
define covered_by(player, c):
    for k in 0..81:
        let j = player * 81 + k
        if gangs[j].sector == GANG_INACTIVE or planning_records[j].family != 6:
            continue
        if aux_records[j].focus == -1:
            if aux_records[j].coverage_sector == c:
                return j
        else if gangs[j].sector == c:
            return j
    return -1

let idx = player * 81 + slot
let g = gangs[idx]
let r = planning_records[idx]
let s = g.sector
let w = sector_weight[player * 64 + s]
if w < 1:
    # selector 0x60: the weight-10 sectors in ascending order, then 100
    let listed = []
    for c in 0..64:
        if sector_weight[player * 64 + c] == 10:
            append(listed, c)
    let dest = -1
    if count(listed) > 0:
        append(listed, 100)
        for each c in listed:
            if covered_by(player, c) == -1:
                dest = c
                break
    let mode = 2
    if dest != -1:
        mode = dest + 0x40
        aux_records[idx].coverage_sector = dest
    let step = select_sector(player, mode, idx)
    plan(idx, ACTION_MOVE, step, 0)
    aux_records[idx].focus = -1
    aux_records[idx].coverage_sector = step
else:
    let kind = 0
    if w == 10 and hostile_owner(player, s):
        kind = 1
    let t = draw_once(player, idx, kind)
    let done = false
    if t != -1:
        plan(idx, ACTION_ATTACK, t / 81, t % 81)
        aux_records[idx].focus = s
        done = true
    if not done:
        let wp = weapon_upgrade(player, idx)
        let ar = armor_upgrade(player, idx)
        if wp != -1 and r.weapon_cooldown <= 0:
            plan(idx, ACTION_EQUIP, wp, 0)
            r.weapon_cooldown = item_definitions[wp].cost * 3
            aux_records[idx].focus = -1
            done = true
        else if ar != -1 and r.armor_cooldown <= 0:
            plan(idx, ACTION_EQUIP, ar, 0)
            r.armor_cooldown = item_definitions[ar].cost * 3
            aux_records[idx].focus = -1
            done = true
    # unreachable while w is positive: Heal needs Force below 8, Heal at least
    # -3, prev not Attack and w == 0; then Control when w < 1 and the gang can
    # take the sector alone, otherwise the guard Move of the w < 1 branch
    if not done:
        let u = draw_target(player, idx, kind, 5)
        plan(idx, ACTION_ATTACK, u / 81, u % 81)
        aux_records[idx].focus = s
if scenario == 0 and turns_remaining() < 4:
    plan(idx, ACTION_TERMINATE, 0, 0)
    r.needs_family = 1
```

## Outputs

`covered_by` returns an index into `gangs` or -1. The handler returns nothing;
it writes the gang's planned action and targets through `plan`, sets `focus` to
the gang's sector after an Attack and to -1 after a Move or an Equip, and sets
`coverage_sector` to the one-step Move destination. An Equip sets the matching
cooldown to three times the item's cost. Draws one `roll` for the first target
draw and up to five more for the second, plus the draws inside
`select_sector`.

## Edge cases

A gang that sees any gang at all never moves: after a failed first draw and no
equipment to buy it attacks the last of up to five further draws even when
every strength test failed, and the strength test can be made on a different
gang from the one attacked (BUG-AI-003). With no sector of weight 10 the gang
wanders through mode 2. When every such sector is covered, the list's end
marker 100 is taken as the destination: mode `0xA4` scores every sector 0, so
the sector selector steps toward a sector drawn from all 64 (FND-AI-059). The
sector a hunter covers is its one-step destination, which is usually not the
hostile sector itself, so a second hunter can pick the same target. The scan
of `covered_by` includes the planning gang itself (FND-AI-059).

## What the sources say

SRC-MANUAL-GOG does not describe the computer players' strategies.

## Differences between builds

None known.

## Open questions

- The record layout behind `aux_records` is uncertain (FND-AI-015).
- No stored coverage sector of 100 was found, so `covered_by(player, 100)` is
  taken to return -1 (FND-AI-059).
