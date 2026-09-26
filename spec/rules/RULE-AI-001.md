---
id: RULE-AI-001
title: A computer player's planning pass rolls its gangs' action history, dispatches every gang, then hires
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-003, FND-AI-019, FND-AI-010, FND-AI-001, FND-AI-009, FND-AI-042, FND-AI-043, FND-AI-044, FND-AI-046, FND-AI-051, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-AI-002, RULE-AI-003, RULE-AI-004, RULE-AI-010, RULE-AI-011, RULE-AI-013, FMT-STATE-001, FMT-STATE-007]
---

## Summary

When a computer player's turn to plan comes, it wipes its planning records if
this is its first pass of the match, remembers its last hire role, shifts each
gang's remembered actions back by one turn, marks empty roster slots so the
next gang there gets a fresh family, looks over the board, stops two of its
gangs in one sector from both carrying on Chaos or both carrying on Influence,
gives every living gang its order through that gang's strategy family, updates
the sector it hires into, and finally decides whether to hire. A player that
took over a dropped network player plans every gang as a raider (family 9).

## When it runs

During `planning_phase`, when the slot being planned is a computer player's.

## Parameters

`player`: the computer player planning.

## Inputs

`ai_started`, `raider_mode`, `hire_role`, `planning_records`, `gangs`
(`sector`, `weapon`, `armor`), and what the rules it calls read.

## Procedure

```text
define reset_planning(idx):
    let r = planning_records[idx]
    r.family = 99
    r.needs_family = 0
    r.older_action = 0
    r.older_target = 0
    r.older_target_2 = 0
    r.previous_action = 0
    r.previous_target = 0
    r.previous_target_2 = 0
    r.planned_action = 0
    r.planned_target = 0
    r.planned_target_2 = 0
    r.weapon_cooldown = 0
    r.armor_cooldown = 0
    return

if ai_started[player] == 0:
    for slot in 0..81:
        reset_planning(player * 81 + slot)
    hire_role[player] = 0
    previous_hire_role[player] = 0
    planning_records[player * 81].needs_family = 1
    ai_started[player] = 1
previous_hire_role[player] = hire_role[player]
for slot in 0..81:
    let idx = player * 81 + slot
    let r = planning_records[idx]
    let g = gangs[idx]
    if g.sector == GANG_INACTIVE:
        r.needs_family = 1
        r.weapon_cooldown = 0
        r.armor_cooldown = 0
        continue
    r.older_action = r.previous_action
    r.older_target = r.previous_target
    r.older_target_2 = r.previous_target_2
    r.previous_action = r.planned_action
    r.previous_target = r.planned_target
    r.previous_target_2 = r.planned_target_2
    r.planned_action = 0
    r.planned_target = 0
    r.planned_target_2 = 0
    if g.weapon == -1:
        r.weapon_cooldown = 0
    else:
        r.weapon_cooldown = r.weapon_cooldown - 1
    if g.armor == -1:
        r.armor_cooldown = 0
    else:
        r.armor_cooldown = r.armor_cooldown - 1
call RULE-AI-003(player)
# no two gangs in one sector carry on Chaos, or carry on Influence
for s in 0..64:
    if previous_action_count(player, s, ACTION_CHAOS) > 1:
        for slot in 0..81:
            let idx = player * 81 + slot
            if gangs[idx].sector == s and planning_records[idx].previous_action == ACTION_CHAOS:
                planning_records[idx].previous_action = ACTION_NONE
                break
    if previous_action_count(player, s, ACTION_INFLUENCE) > 1:
        for slot in 0..81:
            let idx = player * 81 + slot
            if gangs[idx].sector == s and planning_records[idx].previous_action == ACTION_INFLUENCE:
                planning_records[idx].previous_action = ACTION_SNITCH
                break
# the late-match switch to family 9 misses raider_mode (BUG-AI-005) and has no effect
for slot in 0..81:
    let idx = player * 81 + slot
    if gangs[idx].sector != GANG_INACTIVE:
        if raider_mode[player] != 0:
            planning_records[idx].family = 9
        call RULE-AI-002(player, slot)
refresh_anchor(player)
call RULE-AI-010(player)
```

## Outputs

No return value. On the player's first pass resets all 81 records, clears
`hire_role` and `previous_hire_role`, flags slot 0 for a family and sets
`ai_started[player]`. Sets `previous_hire_role[player]` to the hire role of
the previous pass. Rolls the action history and updates the cooldowns of the
active planning records, and sets `needs_family` and clears the cooldowns of
the empty ones. Changes at most one record's previous action per sector for
each of the two cleanups. May set every active gang's family to 9. Then
everything RULE-AI-003, RULE-AI-002 (once per active gang, in roster slot
order), `refresh_anchor` and RULE-AI-010 change. Draws only through the rules
it calls.

## Edge cases

On a player's first pass the history is reset and then rolled, so every gang
plans from previous action None; only slot 0 is flagged for a family, so a
gang in another slot at that moment keeps family 99 and plans nothing until
the slot is emptied and refilled. An inactive record keeps its history until
the slot is reused; the new gang's first dispatch wipes it (RULE-AI-002). A gang
that dies in a turn's combat and whose slot is refilled by the same turn's hire
phase is never seen empty at a pass, so the new gang keeps the dead gang's
family and history.

Only the first matching gang in each sector loses its continuation, so three
gangs carrying on Chaos in one sector become one None and two Chaos. Both
cleanups run for a sector before the next sector.

`raider_mode` is set only when the player took over a network player who left
(RULE-AI-027); with it every active gang is family 9 at every pass, so the
hire role's family lasts only until the next pass. The pass also tests, when
13 or fewer turns remain, whether the player is far behind the leader, and
then writes a byte meant to set `raider_mode`; the store lands 64 bytes past
the flags, so the switch never happens (BUG-AI-005).

## What the sources say

SRC-MANUAL-GOG does not describe how the computer players plan.

## Differences between builds

None known.

## Open questions

None.
