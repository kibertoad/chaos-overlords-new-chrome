---
id: BUG-AI-001
title: The computer players' guards against a second family-6 hire compare the previous hire role with a schedule slot number
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
impact: rules
intent: unintended
player_reliance: unknown
evidence: [FND-AI-050, FND-AI-014, FND-AI-009, FND-AI-002, FND-OBJECTIVE-003]
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
(slot 2) or Dominance (slot 10), numbering the scenarios as FND-OBJECTIVE-003 does.

## Mechanism

Each of those schedule slots writes hire role 4, which the family table of
RULE-AI-002 maps to family 6. When a hostile human gang is visible in a sector
no hunter covers, the hunter test of RULE-AI-010 forces the slot on any turn,
unless its guard fires. The guard compares `previous_hire_role[player]` (the
role of the previous hire, 0 to 6) with a constant equal to the slot number: 6
in Power, Kill 'Em All and Big 40, 5 in Greed and Armageddon, 2 in Acceptance
and 10 in Dominance.

Greed writes only the roles 1, 2, 3, 4 and 6, and Dominance the roles 1 to 6,
so their guards never fire. In Power, Kill 'Em All and Big 40 the guard fires
after a role-6 hire, in Acceptance after a role-2 hire and in Armageddon after
a role-5 hire, none of which is a family-6 hire, and no guard fires after a
role-4 hire. When a guard fires, the slot is not forced that turn, and if the
turn's own slot is the hunter slot it is redirected to another family's slot,
so the player hires no hunter that turn.

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

- The alternative reading, that the guards compare with the slot on purpose to
  avoid some other role after a family-6 hire, is not excluded; no reading
  makes the Dominance comparison with 10 meaningful.
