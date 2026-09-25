---
id: RULE-HIRE-002
title: Vacant hire offers are refilled in place at the player's planning entry
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-HIRE-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-RNG-002]
---

## Summary

When a player's planning starts, each empty offer slot gets a new gang, drawn
at random from the 89 hireable gang types. The new offer is never the same
as another offer on show or as the gang that just left that slot.

## When it runs

At the start of each player's part of `planning_phase`, on both the computer
and the human path, before the player gives any order.

## Parameters

- `player`: the player slot whose offers are refilled.

## Inputs

`hire_offers` for the player's three slots, and the state of `rng` through
`roll`.

## Procedure

```text
for slot in 0..3:
    let old = hire_offers[player * 3 + slot]
    if old < 0:
        let removed = -old
        while true:
            let pick = roll(89)
            let clash = pick == removed
            for other in 0..3:
                let shown = hire_offers[player * 3 + other]
                if shown > 0 and shown == pick:
                    clash = true
            if not clash:
                hire_offers[player * 3 + slot] = pick
                break
```

## Outputs

No return value. Writes a gang definition number from 1 to 89 into each of
the player's offer slots that held a negative value. Makes three draws from
`rng` for each candidate drawn, rejected candidates included, and none when no
slot is vacant.

## Edge cases

- At the first planning entry of a match every slot holds -100 (RULE-HIRE-004),
  so all three are filled, slot 0 first; `removed` is then 100, which no draw
  can give.
- A slot filled earlier in the same call is positive, so later slots avoid it.
- The number of draws is not bounded: each rejected candidate costs another
  three draws.
- Definition 0 is never offered, since `roll(89)` gives 1 to 89.

## What the sources say

SRC-MANUAL-GOG, page 17, says a hired or fired offer is replaced next turn and
that the three offers always differ from each other. The executable agrees,
and in addition never offers the gang just removed from the same slot.

## Differences between builds

None known.

## Open questions

- Whether any other test (gangs other players are offered, gangs the player
  already has) applies to a candidate has not been read; the observed test
  has none.
- The order of the comparisons inside one candidate's test is not recorded;
  it does not change the draws.
