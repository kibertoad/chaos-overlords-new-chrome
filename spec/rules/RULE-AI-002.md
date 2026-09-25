---
id: RULE-AI-002
title: The per-gang AI dispatcher sets the gang's family from scenario and hire role, then runs that family's handler
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-001, FND-AI-002, FND-AI-015]
conflicting: []
split_with: []
related: [FMT-STATE-001, RULE-AI-019, RULE-AI-020, RULE-AI-021, RULE-AI-022, RULE-AI-023, RULE-AI-024, RULE-AI-025, RULE-AI-026, RULE-AI-027, RULE-AI-028, RULE-AI-029, RULE-AI-030, RULE-AI-031]
---

## Summary

Each computer gang belongs to a strategy family. Before the gang plans, the
dispatcher may move it to the family that its player's current hire role
stands for in this scenario; then the family's handler chooses the gang's
order.

## When it runs

In RULE-AI-001, once for each active gang of the computer player, in ascending
roster slot order.

## Parameters

`player`: the computer player planning. `slot`: the gang's roster slot.

## Inputs

`planning_records` (`unk_01`, `family`), `scenario`, `hire_role`, `gangs`
(`sector`).

## Procedure

```text
# one row per scenario, one column per hire role 0 to 6; -1 keeps the current family
let families = [
    0, 0, 3, 2, 6, -1, 7,
    0, 1, 3, 2, 6, 5, 7,
    0, 0, 5, 2, 6, -1, 7,
    0, 0, 5, 2, 6, 3, 7,
    0, 1, 3, 2, 6, 5, 7,
    0, 1, 3, 2, 6, 5, 7,
    0, 13, 14, -1, 6, 5, 7,
    10, 0, 3, 11, 12, -1, 7,
    0, 13, 14, 3, -1, -1, -1,
    0, 1, 3, 2, 6, 3, -1]
let idx = player * 81 + slot
let r = planning_records[idx]
let role = hire_role[player]
if r.unk_01 != 0 and role >= 0 and role <= 6:
    let f = families[scenario * 7 + role]
    if f != -1:
        r.family = f
        if role == 4:
            aux_records[idx].coverage_sector = gangs[idx].sector
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
# after the handler the dispatcher tests the scenario for 6, 7 and 8; see Open questions
```

## Outputs

No return value. May set the record's `family`, and for hire role 4 the gang's
`coverage_sector`. Then whatever the family's handler writes: the gang's
planned action and targets, and for some families a new family, cooldowns and
auxiliary values. Draws only through the handler.

## Edge cases

A cell marked -1 (for example hire role 5 in Greed) and a hire role outside 0
to 6 keep the gang's family, so a gang can keep family 4 or 9, which no cell
assigns. A family with no handler (8, and 99 after a reset) plans nothing, and
the gang keeps the action already in its gang record.

## What the sources say

SRC-MANUAL-GOG does not describe the computer players' strategies.

## Differences between builds

None known.

## Open questions

- What sets byte +1 of the planning record (`unk_01`), which gates the family
  assignment, is not recorded.
- The 16-bit field the hire-role-4 branch writes is taken to be the
  auxiliary record's `coverage_sector` (FND-AI-015); FND-AI-002 describes it as
  the planning record's +12 word.
- After the handler, the dispatcher compares the scenario with 6, 7 and 8, and
  it contains a mode 9 Move for roster slot 0 in some scenario; neither
  branch's effect is recorded.
- The scenario numbering of rows 6 (Eliminate) and 7 (Siege) is contested by
  FND-UI-033 and FND-TURN-003.
- What a family with no handler leaves in the gang record is inferred: the
  dispatcher writes nothing for it.
