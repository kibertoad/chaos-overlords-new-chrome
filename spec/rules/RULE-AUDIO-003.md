---
id: RULE-AUDIO-003
title: Applying the music and effects levels
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AUDIO-001, FND-AUDIO-002, FND-OPTIONS-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: []
---

## Summary

The Music and Sound Effects menus each hold a level from 0 to 10. Level 0 turns
that kind of sound off. Any other level sets the auxiliary volume to the level
times 6400 in each stereo channel, so level 10 is 64000 of a possible 65535.

## When it runs

When the Options helper applies the levels. Its callers are not recorded.

## Parameters

None.

## Inputs

`music_level`, `effects_level`.

## Procedure

```text
if music_level == 0:
    music_enabled = 0
    emit MusicStopped()
else:
    let music_volume: UINT32 = music_level * 25 * 256
    emit MusicVolumeSet(music_volume | (music_volume << 16))
effects_enabled = effects_level != 0
if effects_level != 0:
    let effects_volume: UINT32 = (effects_level * 25) << 8
    emit EffectsVolumeSet(effects_volume | (effects_volume << 16))
```

## Outputs

No return value. Sets `music_enabled` to 0 at music level 0 and sets
`effects_enabled`. Emits `MusicStopped` or `MusicVolumeSet`, then
`EffectsVolumeSet` for a nonzero effects level. Each volume value carries the
left channel in its low 16 bits and the right channel in its high 16 bits, as
`auxSetVolume` takes them.

## Edge cases

Level 5 gives 32000 in each channel and level 10 gives 64000. The initialized
levels are 6 for effects and 5 for music (RULE-OPTIONS-001).

## What the sources say

SRC-MANUAL-GOG, pages 9 and 10, describes the Music and Sound Effects options
as volume sliders from Mute to Loud, each with a default of Medium, and says the
two volumes can differ. The executable's defaults are 5 for music and 6 for
effects.

## Differences between builds

None known.

## Open questions

- Whether a nonzero music level sets `music_enabled` again and restarts the
  current program.
- What the helper does with the volume at effects level 0.
- Whether the music or the effects are applied first, and which auxiliary
  device each volume call addresses.
- Which calls run the helper: the menu commands, startup, or both.
- `music_enabled` has no recorded address.
