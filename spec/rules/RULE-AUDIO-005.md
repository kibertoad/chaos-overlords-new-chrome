---
id: RULE-AUDIO-005
title: Playing a sound effect, which cuts off the one playing
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AUDIO-002, FND-AUDIO-003, FND-AUDIO-006, FND-AUDIO-019, FND-EXE-004, EXP-AUDIO-001, EXP-AUDIO-002]
conflicting: []
split_with: []
related: []
---

## Summary

Sound effects play one at a time: starting one stops whichever effect is
playing. Most effects are played through a wrapper that does nothing while
sound effects are turned off; the turn-start sound bypasses it. Music plays on
its own path and is not affected.

## When it runs

Whenever another rule or a screen plays a sound effect.

## Parameters

None.

## Inputs

`effects_enabled`, `effect_slots`, `effects_suppressed`.

## Procedure

```text
define play_sound(slot):
    # the original also takes a priority and keeps a channel record per call;
    # with one channel and every call at priority 1 they never stop a sound
    if slot >= 0 and slot < 48 and effect_slots[slot] != 0 and effects_suppressed == 0:
        emit EffectPlayed(slot)

define play_effect(slot):
    if effects_enabled != 0:
        play_sound(slot)
```

## Outputs

`play_effect` and `play_sound` return nothing. Each emits `EffectPlayed` with
the slot when the sound starts. The original starts it with
`PlaySoundA(sound, 0, SND_ASYNC | SND_MEMORY | SND_NODEFAULT)`; without
`SND_NOSTOP`, Windows stops any sound `PlaySoundA` is playing and starts the new
one.

## Edge cases

- A slot with no sound loaded, such as slot 5 outside Detailed Combat or a slot
  whose file was missing, plays nothing.
- `effects_suppressed` is never set, so every call with a loaded slot reaches
  `PlaySoundA` (FND-AUDIO-006).
- The sound is played from a copy of the whole file the loader kept in memory.

## What the sources say

None of the sources describes it.

## Differences between builds

None known.

## Open questions

- EXP-AUDIO-001 and EXP-AUDIO-002 reach both outcomes of the test in
  `play_effect`, the push cue passed on with `effects_enabled` set and refused
  with it clear. No run of the original reaches an empty slot or observes one
  effect cutting off another, which rest on FND-AUDIO-003 and FND-AUDIO-006
  alone, so the status stays `supported`.
