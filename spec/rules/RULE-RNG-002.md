---
id: RULE-RNG-002
title: roll(n) gives a whole number from 1 to n from three draws
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-RNG-002, FND-RNG-003, FND-RNG-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-RNG-001]
---

## Summary

Every time the game needs a random number it asks for a whole number from 1 to
some limit. Each such request uses up three numbers from the generator, and the
third decides which of the first two is used.

## When it runs

Whenever a rule calls `roll`. It is the only way the game draws from `rng`: the
runtime `rand` has no other caller (FND-RNG-002), and the 61 calls of the
wrapper cover the computer players, setup, city generation, hiring, the shared
dice and the whole-turn resolver (FND-RNG-003, FND-RNG-004).

## Parameters

`n`: the largest value wanted, an integer.

## Inputs

The state of `rng`, through `draw()` (RULE-RNG-001).

## Procedure

```text
define roll(n) -> INT32:
    let bound = n
    if bound < 1:
        bound = 1
    let first = draw()
    let second = draw()
    let selector = draw()
    let kept = second
    if selector > 0x3FFE:
        kept = first
    return kept % bound + 1
```

## Outputs

Returns an `INT32` from 1 to `bound`. Makes exactly three draws from `rng`.

## Edge cases

An `n` of 0 or below is counted as 1, so `roll(n)` returns 1, and the three
draws are still made.

Each draw is at most 32767, so values above 32768 can never be returned. For a
`bound` that does not divide 32768 the low values come up slightly more often
than the high ones.

A caller that needs a value from 0 to `n - 1` writes `roll(n) - 1`, as the
original does; there is no zero-based form.

## What the sources say

SRC-MANUAL-GOG, numbered page 48, describes the game's random tests as rolls of
six-sided dice in which a 5 or 6 succeeds. It says nothing of the generator or
of the three draws behind each request.

## Differences between builds

None known.

## Open questions

- No run of the original has compared a sequence of results with a
  prediction from a known state.
