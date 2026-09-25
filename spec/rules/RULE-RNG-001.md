---
id: RULE-RNG-001
title: The generator, its step, and its seed at process start
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-RNG-001, FND-RNG-002, FND-RNG-005, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: []
---

## Summary

The game has one random number generator, the C runtime's `rand`. It is seeded
once, when the program starts, from the Windows clock, and every random number
in every match played in that run of the program comes from the same sequence.

## When it runs

The seeding runs once, when the process starts, before the preferences are
loaded (RULE-OPTIONS-001, which may make the first six draws). The step
`rng_step` runs each time a rule makes a `draw()` from `rng`.

## Parameters

None.

## Inputs

`timer_ms` at process start; `rng_state`.

## Procedure

```text
# Once, when the process starts
rng_state = timer_ms & 0xFFFF

# The step behind every draw() from rng: draw() gives rng_step()
define rng_step() -> INT32:
    rng_state = rng_state * 0x343FD + 0x269EC3
    return (rng_state >> 16) & 0x7FFF
```

## Outputs

Seeding sets `rng_state` to a value from 0 to 65535. Each step changes
`rng_state` and returns an `INT32` from 0 to 32767: bits 16 to 30 of the new
state.

## Edge cases

`rng_state` is a `UINT32`, so the multiplication and addition wrap at 32 bits.
Only 65536 seeds are possible, so a run of the program can start from only
65536 different sequences.

Nothing reseeds the generator after process start. Starting a new match, or
loading a saved one, continues the sequence from wherever earlier draws in the
same run left it (FND-RNG-005).

## What the sources say

SRC-MANUAL-GOG says nothing about the generator. Its section on dice (numbered
page 48) describes every random test as a roll of six-sided dice.

## Differences between builds

None known.

## Open questions

- The state is per-thread runtime data. That every draw the game makes runs on
  the thread that seeded the generator has not been shown; a draw on another
  thread would start from the runtime's default state of 1.
- No run of the original has confirmed a predicted sequence from a known seed.
