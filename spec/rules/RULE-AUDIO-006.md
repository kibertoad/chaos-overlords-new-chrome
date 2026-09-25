---
id: RULE-AUDIO-006
title: The turn-start sound
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AUDIO-003]
conflicting: []
split_with: []
related: [RULE-AUDIO-005]
---

## Summary

Each turn after the first begins with a sound, in local games and in network
games of the original's own protocol. It plays even when sound effects are off.

## When it runs

In `turn_start`, after the per-sector financial work and before
`planning_phase`, on every pass of the outer turn loop.

## Parameters

- `first_pass` (`INT32`): nonzero on the first pass of the outer turn loop since
  it was entered.

## Inputs

`local_game`, `network_game`.

## Procedure

```text
if first_pass == 0 and (local_game != 0 or network_game != 0):
    play_sound(9)
```

## Outputs

No return value. Plays slot 9 (`DATA/Snd00208`) through `play_sound`, which
does not test `effects_enabled`.

## Edge cases

The first turn played after the outer turn function is entered is silent.

## What the sources say

None of the sources mentions the sound.

## Differences between builds

None known.

## Open questions

- Whether the outer loop is entered once per match or again after loading a
  save, and so whether the first turn after a load is silent.
- The meanings of `local_game` and `network_game` are read from their use here;
  their writers have not been checked.
- The call passes priority 1; what that changes is not recorded.
