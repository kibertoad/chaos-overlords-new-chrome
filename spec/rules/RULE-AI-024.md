---
id: RULE-AI-024
title: Family-5 computer gangs influence the best Support site in owned land, take sectors or move toward Support
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-072, FND-AI-034, FND-AI-033, FND-AI-026, FND-AI-028, FND-EXE-004, EXP-TURN-020, FND-AI-074, EXP-TURN-049, FND-AI-076, EXP-TURN-089, EXP-TURN-093, EXP-TURN-094]
conflicting: []
split_with: []
related: [RULE-AI-022]
---

## Summary

Family 5 builds Support. It runs the family-3 procedure with Support in place
of Cash and sector selector mode 7 in place of mode 8: heal when hurt,
influence the unfinished site with the most Support in owned land, take the
sector by Control when it can, and otherwise move toward owned land with
Support still to gain.

## When it runs

From RULE-AI-002, for a gang whose family is 5.

## Parameters

`player`: the computer player. `slot`: the gang's roster slot.

## Inputs

What `site_builder` (RULE-AI-022) reads, with `site_definitions` (`support`)
in place of `cash`.

## Procedure

```text
site_builder(player, slot, 1)
```

## Outputs

As RULE-AI-022: the gang's planned action and targets, possibly cooldowns,
auxiliary values of -1 and a family of 11 or 2. Draws as RULE-AI-022.

## Edge cases

Mode 7 also skips sectors where another of the player's gangs is continuing an
Influence (FND-AI-026), so two family-5 gangs rarely gather in one sector.
The family-5 draw at `0x0043AC6A` passes the sector as the strength test's
slot, as family 3's does (BUG-AI-007).

## What the sources say

SRC-MANUAL-GOG does not describe the computer players' strategies.

## Differences between builds

None known.

## Open questions

- EXP-TURN-020 directly checks the weight-10 attack branch after Attack, Hide
  or Move: call 10547 draws the sole human target and the attack resolves. The
  fixture compares the complete draw stream and final state, but does not
  isolate the remaining `site_builder` branches: Heal, best Support site and
  continued Influence, solo Control, mode-7 movement, equipment cooldowns,
  failed attacks, three-Move family changes, action cases without a body and
  the late Greed Terminate override. Those branches remain statically supported;
  the attack check alone does not establish the Support-building procedure.
- FND-AI-034 says the handler has "the same action switch and ending" as family
  3; that the unrecorded cases and the three-Move test are also the same is
  assumed. One difference is recorded: family 5's case for a previous None,
  Control, Equip or Heal has no closing store of -1 in the focus, so its
  Influence keeps the gang's sector and its Control leaves the focus unchanged
  (FND-AI-076).
