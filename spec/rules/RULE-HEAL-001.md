---
id: RULE-HEAL-001
title: Heal rolls four dice plus the gang's Heal and adds each success to Force, up to 10
status: established
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-HEAL-001, FND-TURN-007, FND-AI-007, FND-GANG-001, FND-TURN-001, FND-TURN-004, FND-EXE-004, SRC-MANUAL-GOG, EXP-TURN-059, EXP-TURN-062, EXP-TURN-063, EXP-TURN-068, FND-HEAL-002, FND-UI-021]
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
0 or less. Records no report, and marks the gang for the network update
(FND-HEAL-001).

## Edge cases

- Band 0 and band 1 succeed on 5 or 6; band 2 on 4 to 6. No band loses dice.
- A negative effective Heal of -4 or less gives a pool of 0 or less, which
  rolls nothing.
- A gang already at Force 10 would still roll and use its draws, the cap then
  keeping it at 10 (FND-HEAL-001). The gang card's order menus grey Heal for
  a gang at Force 10 (FND-HEAL-002, FND-UI-021), the group bar's Heal changes
  only gangs below Force 10 (FND-UI-021), a recurring Heal order is cleared at
  `turn_start` once the gang is at Force 10 (FND-TURN-004), and every computer
  Heal plan needs Force below 10 (RULE-AI-019 to RULE-AI-031), so a gang meets
  this case only if its Force rose after the order was given, which no finding
  records.

## What the sources say

SRC-MANUAL-GOG, page 31, describes Heal as restoring Force, and page 48 gives
the roll as 4d6 plus the Heal skill with each success adding one Force, up to
a maximum of 10; page 36 says a gang can heal up to Force 10. Page 48 counts a
5 or a 6 as a success. The executable agrees, and for band 2 also counts a 4.

## Differences between builds

None known.

## Open questions

- No recorded run reaches a Heal by a gang at Force 10, which play does not
  give (see Edge cases); that case rests on the static readings
  [FND-HEAL-001], [FND-HEAL-002] and [FND-UI-021]. The runs of
  EXP-TURN-059, EXP-TURN-062 and EXP-TURN-063 reach the three bands and a Heal
  capped at 10, and EXP-TURN-068 a pool of 0 or less.
