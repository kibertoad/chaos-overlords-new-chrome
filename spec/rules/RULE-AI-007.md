---
id: RULE-AI-007
title: Sector selector mode 0 picks a random neighbouring sector
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-005, FND-AI-028, FND-MOVE-003, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-RNG-002, FMT-STATE-001]
---

## Summary

Mode 0 of the shared sector selector sends a gang to one of the eight sectors
around it, chosen at random. The only direct caller is the Move-capacity
repair of the turn resolver.

## When it runs

When `select_sector` is called with mode 0. The one direct call is in
`fn_00476A94`, the Move-capacity repair, during `move_phase` (FND-AI-028).

## Parameters

None.

## Inputs

The gang's `sector` in `gangs`, and `rng_state` through `roll`.

## Procedure

```text
define random_neighbour(player, idx):
    let src = gangs[idx].sector
    let offsets = [-9, -8, -7, -1, 1, 7, 8, 9]
    while true:
        let d = offsets[roll(8) - 1]
        let c = src + d
        let dx = c % 8 - src % 8
        if c >= 0 and c < 64 and dx >= -1 and dx <= 1:
            return c
```

## Outputs

Returns a sector adjacent to the gang's sector. Makes three raw draws for
each `roll(8)`, one `roll` per attempt, and repeats while the pick wraps past a
row or leaves the city.

## Edge cases

- A gang in a corner has three legal neighbours, so the loop can take several
  attempts; each costs a `roll`.
- The drawn neighbour is not checked against the six-gang limit or anything
  else about the sector [FND-MOVE-003].
- `fn_00476A94`, the Move-capacity repair, stores the result as the gang's new
  destination (RULE-MOVE-002) [FND-MOVE-003].
- FND-MOVE-001 described this call as one draw over all 64 sectors followed by
  one-step routing. The instructions of the mode 0 block show the neighbour
  draw above instead, one `roll(8)` over the eight offsets per attempt
  [FND-MOVE-003].

## What the sources say

No source describes it.

## Differences between builds

None known.

## Open questions

None known.
