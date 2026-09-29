---
id: RULE-CITY-004
title: Each player's Right Hands starts in roster slot 0 in its headquarters at Force 10 with no equipment
status: established
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-CITY-003, FND-SETUP-004, FND-TURN-003, EXP-SETUP-001]
conflicting: []
split_with: []
related: [FMT-STATE-001]
---

## Summary

Every player starts the match with one gang, the Right Hands (gang definition
0), at full Force in the headquarters sector.

## When it runs

Once per new match, inside RULE-SETUP-004, after RULE-CITY-003.

## Parameters

- `assigned`, an `INT32` array giving each player's headquarters sector, as
  RULE-CITY-003 returns it.

## Inputs

None beyond the parameter.

## Procedure

```text
for each player in turn_order:
    let g = gangs[player * 81]
    g.player = player
    g.definition = 0
    g.sector = assigned[player]
    g.force = 10
    g.weapon = -1
    g.armor = -1
    g.misc = -1
```

## Outputs

No return value. Fills roster slot 0 of every player. Makes no draws.

## Edge cases

None known.

## What the sources say

SRC-MANUAL-GOG, page 14 (Eliminate), says each player starts with one gang,
the Right Hands.

## Differences between builds

None known.

## Open questions

None known. EXP-SETUP-001 found each player's Right Hands in roster slot 0
with definition 0, Force 10 and -1 in all three equipment bytes.
