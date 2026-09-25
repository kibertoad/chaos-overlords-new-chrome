---
id: RULE-AI-007
title: Sector selector mode 0 picks a random neighbouring sector
status: disputed
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-005, FND-AI-028]
conflicting: [FND-MOVE-001]
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

A gang in a corner has three legal neighbours, so the loop can take several
attempts; each costs a `roll`.

## What the sources say

No source describes it.

## Differences between builds

None known.

## Open questions

- Disputed. FND-AI-005 describes mode 0 as a uniform draw among the eight
  neighbours with rejection of illegal results. FND-MOVE-001 describes the
  same call from `fn_00476A94` as scoring nothing, drawing once among all 64
  sectors tied at 0, and routing one step toward the drawn sector with the
  six-gang limit, as the nonzero modes do (RULE-AI-006). The instructions of the
  mode 0 block need to be recorded to settle it.
- Whether the neighbour draw is `roll(8)` over the eight offsets and retried on
  rejection, or draws the row and column offsets separately, is not recorded.
- `fn_00476A94` is the Move-capacity repair helper of the turn resolver; its
  rule belongs to the MOVE area.
