---
id: RULE-AUDIO-006
title: The turn-start sound
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AUDIO-003, FND-AUDIO-006, FND-NET-004, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-AUDIO-005]
---

## Summary

In a network game of the original's own protocol, each turn after the first
begins with a sound, on the hosting computer and on every computer that joined.
A game started with New Game has no turn-start sound. The sound plays even when
sound effects are off.

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

- The first turn played after the outer turn function is entered is silent.
- `local_game` is set only by the Join command and `network_game` only by Host
  and by resuming a saved network game; New Game clears both. Despite its name,
  `local_game` does not mark a game on one computer (FND-AUDIO-006).

## What the sources say

None of the sources mentions the sound.

## Differences between builds

None known.

## Open questions

- Whether the outer loop is entered once per match or again after loading a
  save, and so whether the first turn after a load is silent.
- Whether every saved network game is resumed through the path that sets
  `network_game` has not been followed past the load dispatcher.
