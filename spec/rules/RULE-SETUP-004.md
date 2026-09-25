---
id: RULE-SETUP-004
title: A new match draws the computer players' reactions, then generates the city, the headquarters and the Right Hands, then applies the name modifiers
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-RNG-005, FND-AI-006, FND-CITY-001, FND-CITY-002, FND-CITY-003, FND-SETUP-003, FND-SETUP-004, FND-SETUP-011, FND-RESEARCH-002]
conflicting: []
split_with: []
related: [RULE-RNG-002, RULE-CITY-001, RULE-CITY-002, RULE-CITY-003, RULE-CITY-004, RULE-SETUP-005, RULE-SETUP-006, RULE-SETUP-007, RULE-RESEARCH-002]
---

## Summary

Setting up a new match gives each player a reaction value used by the
computer players, builds the city, places the six headquarters and their
Right Hands, and then applies any name modifiers. Every random draw of a new
match after the portraits happens here, in a fixed order.

## When it runs

Once per new match, called by RULE-SETUP-001 between the two cash writes.

## Parameters

None.

## Inputs

`mentality`, `scenario`, and the state of `rng` through `roll`.

## Procedure

```text
# Reactions, used by the computer players' attitudes. Homicidal Maniac
# (mentality 3) makes no draw.
for each player in turn_order:
    if mentality != 3:
        reaction[player] = roll(4) + 2
    else:
        reaction[player] = 0

call RULE-CITY-001()
call RULE-CITY-002()
let assigned = call RULE-CITY-003()
call RULE-CITY-004(assigned)

# None of the following draws from rng
call RULE-SETUP-006()
call RULE-SETUP-007()
call RULE-SETUP-005()
call RULE-RESEARCH-002()
```

## Outputs

No return value. Sets `reaction`, every sector's Income, Tolerance, sites and
headquarters owners, the Right Hands and any extra starting gangs,
`modifier_visibility`, the permanent Crackdowns of the island modifier, and
`research_remaining`. The draws are, in order: one `roll(4)` per player
unless the Mentality is Homicidal Maniac, then those of RULE-CITY-001,
RULE-CITY-002 and RULE-CITY-003.

## Edge cases

At Homicidal Maniac the first draw of this rule is the first density centre
of RULE-CITY-001.

## What the sources say

SRC-MANUAL-GOG, page 14, says each player starts Eliminate with one gang, the
Right Hands, and says nothing of how the city is made.

## Differences between builds

None known.

## Open questions

- The address of `reaction` is not recorded, and the initialization of the
  computer players' attitude matrix, which happens in the same initializer, is
  described with the computer players.
- The order of the three name-modifier steps and the research initialization
  among themselves and after RULE-CITY-004 is not recorded; none of them draws
  from `rng` or reads what another writes, so the result does not depend on
  it. FND-SETUP-003 places the island modifier after the Right Hands.
- Whether the names are compared at the point each modifier is applied, as
  written here, or in one earlier scan that sets transient flags, is not
  recorded; since the names do not change in between, the result is the same.
