---
id: RULE-VIDEO-001
title: The intro plays the logos movie and then the intro movie, each ended by the left button
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-VIDEO-002, FND-VIDEO-001, FND-PLATFORM-012, FND-UI-023, FND-EXE-004]
conflicting: []
split_with: []
related: [FMT-VIDEO-001, RULE-AUDIO-003]
---

## Summary

At every start of the game, unless it was started with a saved game that
loads, the screen turns black and the logos movie plays, then the intro movie,
in a 480-by-256 frame at `(80,102)`. Each movie's sound plays at the Sound
Effects level. Holding the left mouse button ends the movie playing; keys do
nothing. A movie that is missing or does not open is skipped.

## When it runs

Once, during the title initialization, after the options, the sound setup and
the timers are set up and before the title screen is shown, when no saved game
named at startup has loaded.

## Parameters

None.

## Inputs

`effects_level`, `left_button_down`, `intro_tick_pending`, and the movie files `DATA/MVLOGOS` and
`DATA/MVINTRO` (FMT-VIDEO-001).

## Procedure

```text
clock intro_tick: 10 Hz

define play_movie(name):
    emit MovieAreaCleared()
    let opened = movie_open(name)
    if opened == 0:
        return
    movie_set_volume(effects_level * 25)
    let frame = 0
    let playing = 1
    while playing != 0:
        # at most one frame per pass, when the movie's own frame time has come
        if movie_frame_due() != 0:
            emit MovieFrameShown(frame)
            if frame == movie_frame_count():
                playing = 0
            else:
                frame = frame + 1
        # one window message is handled per pass
        if intro_tick_pending != 0:
            intro_tick_pending = 0
            if left_button_down != 0:
                playing = 0

emit ScreenFilledBlack()
play_movie("Data\\mvLogos")
play_movie("Data\\mvIntro")
emit MovieAreaCleared()
```

`movie_open`, `movie_set_volume`, `movie_frame_due` and `movie_frame_count`
stand for the calls into the Smacker library the game ships
(FND-PLATFORM-006): opening the file with every sound track, setting the volume
of all tracks at the centre pan, the library's wait test, and the frame count of
the file's header.

## Outputs

No return value. Emits `ScreenFilledBlack` for the rectangle `(0,0)-(640,460)`,
`MovieAreaCleared` for the rectangle `(80,102)-(560,358)` before each movie and
after the second, and `MovieFrameShown` for each frame drawn at `(80,102)`. Plays
each movie's sound at a library volume of `effects_level * 25 * 256`.

## Edge cases

- A press and release of the left button that both fall between two ticks of
  `intro_tick` are missed; the button must be held when a tick comes.
- A press ends only the movie playing. A button still held when the second
  movie starts ends it at the first tick.
- With `effects_level` 0 the movies play without sound.
- Each movie closes after the pass that shows the frame whose number equals the
  frame count, one pass after its last frame.
- When a movie changes its palette, the frame is remapped to the palette of the
  library's buffer before it is drawn; in the 8-bit display set that fits the
  movie to the screen palette.
- The path of each movie starts with the prefix the startup drive check leaves
  (RULE-AUDIO-010): `.\` when that check ends on its first pass, as it does
  when the game runs from a fixed drive.
- The game raises its thread priority to the highest level while the intro
  plays and sets it back to normal afterwards.

## What the sources say

SRC-MANUAL-GOG does not describe the intro or how to skip it.

## Differences between builds

None known.

## Open questions

- What the library's blit type 3 and its handling of a volume above its normal
  level do has not been observed.
- How the Smacker library of the GOG build fits the movie's palette in the
  8-bit display set has not been captured.
