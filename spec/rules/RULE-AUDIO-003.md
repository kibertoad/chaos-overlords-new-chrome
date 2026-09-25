---
id: RULE-AUDIO-003
title: Applying the music and effects levels
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AUDIO-001, FND-AUDIO-002, FND-AUDIO-007, FND-OPTIONS-001, FND-EXE-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: []
---

## Summary

The Music and Sound Effects menus each hold a level from 0 to 10. Level 0 turns
that kind of sound off. The effects level is applied first: it sets an
auxiliary volume of the level times 6400, 0 at level 0. Then a music level of
0 stops the music, and any other level turns music on again and sets the CD
audio volume to the level times 6400, so level 10 is 64000 of a possible 65535.
From level 6 up the right channel is one lower than the left.

## When it runs

When the Options helper applies the levels: once in the title initialization,
after either level is chosen from the Music or Sound Effects menu, and when the
game's window becomes active again (RULE-AUDIO-002).

## Parameters

None.

## Inputs

`music_level`, `effects_level`.

## Procedure

```text
effects_enabled = effects_level != 0
let effects_volume: INT32 = (effects_level * 25 * 256) % 65536
# the 16-bit channel value is sign-extended before the two halves are added
if effects_volume >= 32768:
    effects_volume = effects_volume - 65536
emit EffectsVolumeSet((effects_volume * 65536 + effects_volume) % 4294967296)
if music_level == 0:
    music_enabled = 0
    emit MusicStopped()
else:
    music_enabled = 1
    let music_volume: INT32 = (music_level * 25 * 256) % 65536
    if music_volume >= 32768:
        music_volume = music_volume - 65536
    emit MusicVolumeSet((music_volume * 65536 + music_volume) % 4294967296)
```

## Outputs

No return value. Sets `effects_enabled` and `music_enabled`. Emits
`EffectsVolumeSet`, then `MusicStopped` or `MusicVolumeSet`. Each volume value
carries the left channel in its low 16 bits and the right channel in its high 16
bits, as `auxSetVolume` takes them. It also puts the check mark on the chosen
item of each menu. It does not start a program: the next poll of
RULE-AUDIO-002 does that once music is enabled.

## Edge cases

- Level 5 gives 32000 in each channel. Level 6 gives 38400 on the left and
  38399 on the right, and level 10 gives 64000 and 63999, because the
  sign-extended channel borrows one from the high half.
- The initialized levels are 6 for effects and 5 for music (RULE-OPTIONS-001).
- The music is stopped with a fade of 32 steps of 17 ms when the CD device
  supports a volume, and at once otherwise.
- The effects volume goes to the auxiliary device the sound setup finds by the
  technology value `0x20`, which no Windows device reports, so it goes to
  device 0 on most machines; the music volume goes to the CD audio device
  (FND-AUDIO-007).
- The game saves both auxiliary volumes at startup and puts them back when the
  player exits from the title.

## What the sources say

SRC-MANUAL-GOG, pages 9 and 10, describes the Music and Sound Effects options
as volume sliders from Mute to Loud, each with a default of Medium, and says the
two volumes can differ. The executable's defaults are 5 for music and 6 for
effects.

## Differences between builds

None known.

## Open questions

- Whether the technology value `0x20` was meant for another device kind cannot
  be told from the executable.
