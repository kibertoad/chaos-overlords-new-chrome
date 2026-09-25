---
id: BUG-AI-001
title: The computer players' guards against a second family-6 hire compare the previous hire role with a schedule slot number
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
impact: rules
intent: unintended
player_reliance: unknown
evidence: [FND-AI-014, FND-AI-009, FND-AI-002]
conflicting: []
split_with: []
related: [RULE-AI-010, RULE-AI-002]
---

## Symptom

A computer player can hire family-6 gangs (the hunters) on consecutive hires
where the schedule looks meant to prevent it, and in some scenarios is kept
from a family-6 hire after an unrelated hire instead.

## Trigger conditions

A computer player reaches the family-6 slot of its scenario's hire schedule in
Power, Kill 'Em All, Big 40 (slot 6), Greed, Armageddon (slot 5), Acceptance
(slot 2) or Dominance (slot 10), numbering the scenarios as FND-AI-002 does.

## Mechanism

Each of those schedule slots writes hire role 4, which the family table of
RULE-AI-002 maps to family 6. The guard on the slot compares
`previous_hire_role[player]` (the role of the previous hire, 0 to 6) with a
constant equal to the slot number: 6 in Power, Kill 'Em All and Big 40, 5 in
Greed and Armageddon, 2 in Acceptance and 10 in Dominance. The role is never
10, so the Dominance guard never fires. In the other scenarios the guard fires
after a hire of role 6, 5 or 2, which is not a family-6 hire, and never after a
role-4 hire.

## Frequency

Every time a computer player's schedule reaches the guarded slot in one of the
listed scenarios, which the schedule does at fixed turn positions.

## Player reliance

Unknown. The effect is on how many hunters the computer players hire; no
player strategy that depends on it is recorded.

## Fixes elsewhere

None known.

## Differences between builds

None known.

## Open questions

- What the guard does when it fires (skip the slot, or take another slot) is
  not recorded in the findings (FND-AI-009).
- The alternative reading, that the guards compare with the slot on purpose to
  avoid some other role after a family-6 hire, is not excluded; no reading
  makes the Dominance comparison with 10 meaningful.
- The scenario numbering behind the scenario names is contested (see
  RULE-AI-002).
