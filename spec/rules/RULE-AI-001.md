---
id: RULE-AI-001
title: A computer player's planning pass rolls its gangs' action history, dispatches every gang, then hires
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-003, FND-AI-019, FND-AI-010, FND-AI-001, FND-AI-009]
conflicting: []
split_with: []
related: [RULE-AI-002, RULE-AI-003, RULE-AI-004, RULE-AI-010, RULE-AI-013, FMT-STATE-001]
---

## Summary

When a computer player's turn to plan comes, it first shifts each gang's
remembered actions back by one turn, then looks over the board, stops two of
its gangs in one sector from both carrying on Chaos or both carrying on
Influence, gives every living gang its order through that gang's strategy
family, updates the sector it hires into, and finally decides whether to hire.

## When it runs

During `planning_phase`, when the slot being planned is a computer player's.

## Parameters

`player`: the computer player planning.

## Inputs

`ai_started`, `planning_records`, `gangs` (`sector`, `weapon`, `armor`), and
what the rules it calls read.

## Procedure

```text
define reset_planning(idx):
    let r = planning_records[idx]
    r.family = 99
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
    ai_started[player] = 1
else:
    for slot in 0..81:
        let idx = player * 81 + slot
        let r = planning_records[idx]
        let g = gangs[idx]
        if g.sector == GANG_INACTIVE:
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
for s in 0..64:
    if previous_action_count(player, s, ACTION_INFLUENCE) > 1:
        for slot in 0..81:
            let idx = player * 81 + slot
            if gangs[idx].sector == s and planning_records[idx].previous_action == ACTION_INFLUENCE:
                planning_records[idx].previous_action = ACTION_SNITCH
                break
for slot in 0..81:
    # family 9 is set on some records here; see Open questions
    if gangs[player * 81 + slot].sector != GANG_INACTIVE:
        call RULE-AI-002(player, slot)
refresh_anchor(player)
call RULE-AI-010(player)
```

## Outputs

No return value. Rolls the action history and updates the cooldowns of the
player's active planning records, or resets all 81 records on the player's
first pass and sets `ai_started[player]`. Changes at most one record's
previous action per sector for each of the two cleanups. Then everything
RULE-AI-003, RULE-AI-002 (once per active gang, in roster slot order),
`refresh_anchor` and RULE-AI-010 change. Draws only through the rules it calls.

## Edge cases

On a player's first pass the history is reset instead of rolled, so every gang
plans from previous action None. An inactive record keeps its history until
the slot is reused. Only the first matching gang in each sector loses its
continuation, so three gangs carrying on Chaos in one sector become one None
and two Chaos.

## What the sources say

SRC-MANUAL-GOG does not describe how the computer players plan.

## Differences between builds

None known.

## Open questions

- The condition under which the pass sets family 9 before the dispatch is not
  recorded (FND-AI-003).
- `reset_planning` also clears "the other per-record planning fields"; which
  bytes those are (offsets +1, +11 and +15) is not recorded.
- The per-player state the first pass initializes besides the records is not
  recorded.
- When and how a reused roster slot's record is reset (so that a hired gang
  does not inherit the previous occupant's family and history) is not
  recorded; the record of an inactive gang is left alone here.
- Whether the Influence cleanup runs after the whole Chaos cleanup, as
  written, or sector by sector alongside it is not recorded.
- The pass calls a 64-entry sector pass before the dispatch (FND-AI-003); it
  is taken to be the cleanup loops above, or part of RULE-AI-003.
