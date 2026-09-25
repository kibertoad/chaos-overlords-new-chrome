---
id: RULE-HEAL-001
title: Heal rolls four dice plus the gang's Heal and adds each success to Force, up to 10
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-007, FND-GANG-001, FND-TURN-001, FND-TURN-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-RNG-002, FMT-STATE-001]
---

## Summary

A healing gang rolls four dice plus one die per point of its Heal. Each
success gives back one point of Force, and Force never goes above 10.

## When it runs

During `instant_phase`, for each gang whose `action` is `ACTION_HEAL`, at that
gang's place in the phase's player and roster order.

## Parameters

- `player`: the player slot that owns the gang.
- `gang`: the acting gang, FMT-STATE-001.

## Inputs

The gang's `heal` (the effective value rebuilt at `turn_start`) and `force`;
`difficulty_band[player]`; and the state of `rng` through `roll`.

## Procedure

```text
let threshold = 5
if difficulty_band[player] == 2:
    threshold = 4
let pool = gang.heal + 4
let successes = 0
for d in 0..pool:
    if roll(6) >= threshold:
        successes = successes + 1
gang.force = min(gang.force + successes, 10)
```

## Outputs

No return value. Raises the gang's `force` by its successes, to at most 10.
Makes one `roll(6)`, three draws from `rng`, for each die of the pool of
`heal + 4`, even when the gang is already at Force 10, and none for a pool of
0 or less.

## Edge cases

- Band 0 and band 1 succeed on 5 or 6; band 2 on 4 to 6. No band loses dice.
- A negative effective Heal of -4 or less gives a pool of 0 or less, which
  rolls nothing.
- A recurring Heal order is cleared at `turn_start` once the gang is at Force
  10 (FND-TURN-004), so a gang at full Force normally has no Heal order to
  carry out.

## What the sources say

SRC-MANUAL-GOG, page 31, describes Heal as restoring Force, and page 48 gives
the roll as 4d6 plus the Heal skill with each success adding one Force, up to
a maximum of 10; page 36 says a gang can heal up to Force 10. Page 48 counts a
5 or a 6 as a success. The executable agrees, and for band 2 also counts a 4.

## Differences between builds

None known.

## Open questions

- Whether the Heal case tests Force before rolling, and so skips the draws for
  a gang already at 10, is not recorded.
- The instruction addresses of the Heal case in `0x00472775` are not
  recorded.
- Whether Heal records any report is not recorded; FND-EVENT-001 lists no
  Heal report type.
