---
id: RULE-CITY-001
title: A new city's sector Income comes from a random density field, and its starting Tolerance is 17 minus the Income
status: established
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-CITY-001, FND-RNG-005, EXP-SETUP-001, FND-CONTROL-001, FND-PLATFORM-003, FND-UI-035]
conflicting: []
split_with: []
related: [RULE-RNG-002, FMT-STATE-002]
---

## Summary

The game scatters 40 random centres over a 32-by-32 grid and piles density
around each. Each of the 64 sectors covers a 4-by-4 block of the grid; the
denser its block, the higher its Income (3 to 7) and the lower its starting
Tolerance (14 down to 10).

## When it runs

Once per new match, inside RULE-SETUP-004, after the reaction draws and before
the sites are placed.

## Parameters

None.

## Inputs

The state of `rng` through `roll`.

## Procedure

```text
let density: INT32[] = []
for i in 0..1024:
    append(density, 0)

for c in 0..40:
    let cx = roll(32) - 1
    let cy = roll(32) - 1
    for radius in 0..4:
        for x in (cx - radius)..(cx + radius):
            for y in (cy - radius)..(cy + radius):
                if x >= 0 and x < 32 and y >= 0 and y < 32:
                    density[y * 32 + x] = min(4, density[y * 32 + x] + 1)

for s in 0..64:
    let sum = 0
    for x in ((s % 8) * 4)..((s % 8) * 4 + 4):
        for y in ((s / 8) * 4)..((s / 8) * 4 + 4):
            sum = sum + density[y * 32 + x]
    let income = (sum * 10 / 16 + 5) / 10 + 3
    sectors[s].income = income
    sectors[s].tolerance = 17 - income
```

## Outputs

No return value. Sets `income` and `tolerance` of all 64 sectors. Makes 80
calls of `roll(32)`, 240 draws from `rng`, always.

## Edge cases

A sector whose block is at the cap of 4 everywhere has Income 7 and Tolerance
10; an empty block gives Income 3 and Tolerance 14. Footprints near the edge
of the grid are clipped, not wrapped.

## What the sources say

SRC-MANUAL-GOG describes sector Income and Tolerance as properties of each
sector and does not say how a new city gets them.

## Differences between builds

None known.

## Open questions

- The finding gives the footprints as 2 by 2, 4 by 4 and 6 by 6 over four
  passes of growing radius, and says the average is "rounded". The bounds and
  the rounding written here give all 64 Incomes of EXP-SETUP-001 from its 40
  recorded centres, in `base_income` and `income`, and its starting
  Tolerances in `base_tolerance`; one run cannot show that every bound and the
  rounding of an exact half are right.
