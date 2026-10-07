---
id: RULE-AI-002
title: The per-gang AI dispatcher sets the gang's family from scenario and hire role, then runs that family's handler
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-001, FND-AI-002, FND-AI-015, FND-AI-041, FND-AI-042, FND-AI-081, FND-EXE-004, FND-OBJECTIVE-003, FND-AI-009, FND-AI-013, FND-AI-014, FND-AI-019, FND-AI-021, FND-HIRE-002, FND-PLATFORM-003, FND-STATE-006, FND-STATE-007, FND-UI-036, EXP-TURN-016, EXP-TURN-001, EXP-TURN-012, EXP-TURN-013, EXP-TURN-014, EXP-TURN-023, EXP-TURN-026, EXP-TURN-028, EXP-TURN-038, EXP-TURN-039, EXP-TURN-040, EXP-TURN-055]
conflicting: []
split_with: []
related: [FMT-STATE-001, RULE-AI-019, RULE-AI-020, RULE-AI-021, RULE-AI-022, RULE-AI-023, RULE-AI-024, RULE-AI-025, RULE-AI-026, RULE-AI-027, RULE-AI-028, RULE-AI-029, RULE-AI-030, RULE-AI-031]
---

## Summary

Each computer gang belongs to a strategy family. A gang new to its roster slot
has its planning record wiped and takes the family that its player's current
hire role stands for in this scenario; every other gang keeps its family. Then
the family's handler chooses the gang's order.

## When it runs

In RULE-AI-001, once for each active gang of the computer player, in ascending
roster slot order.

## Parameters

`player`: the computer player planning. `slot`: the gang's roster slot.

## Inputs

`planning_records` (`needs_family`, `family`), `scenario`, `elapsed_turns`,
`hire_role`, `gangs` (`sector`).

## Procedure

```text
# seven values per scenario, one per hire role 0 to 6, scenario by scenario; -1 assigns nothing
table role_families: INT8[70] from "RULE-AI-002.role_families.csv"
let idx = player * 81 + slot
let r = planning_records[idx]
let s = gangs[idx].sector
if r.needs_family != 0:
    r.family = 99
    r.needs_family = 0
    # the six target bytes of the three triplets are cleared as well
    r.older_action = ACTION_NONE
    r.previous_action = ACTION_NONE
    r.planned_action = ACTION_NONE
    r.weapon_cooldown = 0
    r.armor_cooldown = 0
    if scenario == 8 and elapsed_turns == 0:
        hire_role[player] = 1
    let role = hire_role[player]
    if role >= 0 and role <= 6:
        let f = role_families[scenario * 7 + role]
        if f != -1:
            r.family = f
            if role == 4:
                aux_records[idx].coverage_sector = s
if r.family == 0:
    call RULE-AI-019(player, slot)
else if r.family == 1:
    call RULE-AI-020(player, slot)
else if r.family == 2:
    call RULE-AI-021(player, slot)
else if r.family == 3:
    call RULE-AI-022(player, slot)
else if r.family == 4:
    call RULE-AI-023(player, slot)
else if r.family == 5:
    call RULE-AI-024(player, slot)
else if r.family == 6:
    call RULE-AI-025(player, slot)
else if r.family == 7:
    call RULE-AI-026(player, slot)
else if r.family == 9:
    call RULE-AI-027(player, slot)
else if r.family == 10:
    call RULE-AI-028(player, slot)
else if r.family == 11:
    call RULE-AI-029(player, slot)
else if r.family == 12:
    call RULE-AI-030(player, slot)
else if r.family == 13 or r.family == 14:
    call RULE-AI-031(player, slot)
# the block after the handler never runs (see Edge cases)
```

## Outputs

No return value. For a record with `needs_family` set: resets the record, may
set its `family`, and for hire role 4 the gang's `coverage_sector`; in Big Man
on the first turn sets `hire_role[player]` to 1. Then whatever the family's
handler writes: the gang's planned action and targets, and for some families a
new family, cooldowns and auxiliary values. Draws only through the handler.

## Edge cases

A cell marked -1 (for example hire role 5 in Greed) and a hire role outside 0
to 6 leave the new gang in family 99 after the reset, and a family with no
handler (8 and 99) plans nothing, so the gang keeps the action already in its
gang record. A gang whose `needs_family` is clear keeps its family whatever
the hire role, so the table applies only to a gang's first plan in its slot,
and to a gang the Greed Terminate branches flagged. No cell assigns family 4
or 9; family 9 comes only from RULE-AI-001.

In Big Man the forced hire role on the first turn gives every gang of the
first pass the family of role 1 (13). The hire role stays 1 until the pass's
hire choice writes a new one.

After the handler, the dispatcher runs a further block only when a per-slot
value that is 0 in every match makes a Force test pass; it therefore never
runs, and the Move through sector selector mode 9 for the Right Hands in
Eliminate (scenario 7) and the Attack or Hide it holds are never ordered there
(FND-AI-041).

## What the sources say

SRC-MANUAL-GOG does not describe the computer players' strategies.

## Differences between builds

None known.

## Open questions

The scenario values are 0 Greed, 1 Power, 2 Acceptance, 3 Dominance,
4 Kill 'Em All, 5 Big 40, 6 Siege, 7 Eliminate, 8 Big Man and 9 Armageddon
(FND-OBJECTIVE-003).

- Reach was measured over every replayed run (DECISIONS.md, 2026-10-06). The
  runs reach the reset of a flagged record and the gangs that keep their
  family; 53 of the 70 cells of the table, in every scenario (EXP-TURN-028
  Greed, EXP-TURN-023 Power, EXP-TURN-039 Acceptance, EXP-TURN-038 Dominance,
  EXP-TURN-001 Kill 'Em All, EXP-TURN-055 Big 40, EXP-TURN-012 Siege,
  EXP-TURN-013 Eliminate, EXP-TURN-014 Big Man, EXP-TURN-026 Armageddon);
  hire role 4 storing the coverage sector; Big Man's first-turn forcing of
  hire role 1 (EXP-TURN-014 and eight other runs); and the handlers of
  families 0 to 7 and 9 to 14, families 4 and 9 through writes (EXP-TURN-040).
- No run reaches these cells: Greed roles 4 and 5, Power 2, Acceptance 5,
  Dominance 4, Kill 'Em All 2, Big 40 roles 2 and 4, Siege 3 and 4, Eliminate
  3 and 5, Big Man 4 to 6, and Armageddon 2 and 6. Among them are all eight
  cells marked -1, so no run reaches a new gang left in family 99 by the
  table. No run reaches a hire role outside 0 to 6, or the dispatch of a
  family with no handler (8 or 99). These rest on FND-AI-002 and FND-AI-001.
  The role schedule of RULE-AI-010 does not give some of these roles in some
  scenarios, so a run would need a write of the player's hire role before a
  Done press that the probe does not make; a `--families` write of 8 or 99
  would reach the dispatch without a handler. Until runs reach them, the
  entry stays `supported`.
