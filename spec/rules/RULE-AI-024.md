---
id: RULE-AI-024
title: Family-5 computer gangs influence the best Support site in owned land, take sectors or move toward Support
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-034, FND-AI-033, FND-AI-026, FND-AI-028]
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

## What the sources say

SRC-MANUAL-GOG does not describe the computer players' strategies.

## Differences between builds

None known.

## Open questions

- FND-AI-034 says the handler has "the same action switch and ending" as family
  3; that the unrecorded cases and the three-Move test are also the same is
  assumed.
