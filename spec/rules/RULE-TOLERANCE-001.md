---
id: RULE-TOLERANCE-001
title: At the start of each resolution a sector's base Tolerance moves one point toward 17 minus its base Income
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-TOLERANCE-001, FND-STATE-001, FND-TURN-008, FND-CITY-001, FND-EXE-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [FMT-STATE-002, RULE-TURN-002, RULE-TOLERANCE-002, RULE-SITE-001, RULE-CITY-001]
---

## Summary

A sector's base Tolerance starts at 17 minus its base Income. After a Bribe or
a Snitch has moved it, it creeps back by one point each turn. Sites do not take
part in the step: their Tolerance is added on top of the base when the sector
record is rebuilt before planning (RULE-SITE-001), so the Tolerance the player
sees moves back toward its normal value as modified by the sites.

## When it runs

Once per turn, at the start of `resolution`, before `instant_phase`
[FND-TOLERANCE-001, FND-TURN-008].

## Parameters

None.

## Inputs

Each sector's `base_income` and `base_tolerance`.

## Procedure

```text
for each sector in sectors:
    let normal = 17 - sector.base_income
    if sector.base_tolerance < normal:
        sector.base_tolerance = sector.base_tolerance + 1
    else if sector.base_tolerance > normal:
        sector.base_tolerance = sector.base_tolerance - 1
```

## Outputs

No return value. Moves each sector's `base_tolerance` one point toward
`17 - base_income`, or leaves it when it is already there. A sector it changes
is also marked for the network update of FND-TOLERANCE-001. Makes no random
draw.

## Edge cases

- The step is one point whatever the distance, so several Bribes take as many
  turns to wear off.
- The step runs before the turn's Bribes and Snitches, and the clamp of
  RULE-TOLERANCE-002 after them, so a base Tolerance of 40 in a sector whose
  normal is 12 is 39 after the step and can be raised back to 40 by one Bribe
  in the same turn.
- `tolerance`, the value the Chaos pass compares with, is rebuilt from
  `base_tolerance` only before planning, so the step made at the start of a
  resolution reaches the Chaos test of the next turn, not this one.
- A sector whose sites change owner or lose progress keeps its base Tolerance;
  only the site part of `tolerance` changes at the next rebuild.

## What the sources say

SRC-MANUAL-GOG, page 48, gives each sector class its Income and base
Tolerance, the two adding up to 17. Page 29 and page 50 say Tolerance changed
by Bribe or Snitch returns to the sector's normal Tolerance, as modified by its
sites, by one point each turn. Page 41 says sites can raise or lower a
sector's Tolerance. The executable agrees: the base returns to 17 minus the
Income, and the sites are added on top.

## Differences between builds

None known.

## Open questions

None known.
