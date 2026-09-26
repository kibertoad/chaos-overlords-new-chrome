---
id: RULE-AUDIO-002
title: Music repeats its program when it ends and pauses while the window is inactive
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AUDIO-001, FND-AUDIO-007, FND-UI-023, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-AUDIO-001, RULE-AUDIO-003, RULE-UI-008]
---

## Summary

About six times a second the game asks MCI whether the CD is playing. When it
is not, music is enabled and the window is active, the current program starts
again from its first track; this is how a program repeats after its last
track. When the game's window loses focus the music pauses, and when the window
regains focus the levels are applied again and the music resumes from where it
stopped. The notification MCI sends at the end of playback changes nothing.

## When it runs

In the main event pump: the poll on each `presentation_tick` the pump takes,
and the pause and resume when the pump handles the window's deactivation or
activation.

## Parameters

- `message` (`INT32`): 0 for the poll, 1 for the window becoming inactive, 2
  for the window becoming active.

## Inputs

`music_mode`, `music_enabled`, `music_playing`, `window_inactive`.

## Procedure

```text
if message == 0:
    if music_enabled != 0 and music_playing == 0 and window_inactive == 0:
        call RULE-AUDIO-001(music_mode)
else if message == 1:
    if music_enabled != 0:
        emit MusicPaused()
    window_inactive = 1
else if message == 2:
    if music_enabled != 0:
        call RULE-AUDIO-003()
        emit MusicResumed()
    window_inactive = 0
```

## Outputs

No return value. Starts the current program again, or emits `MusicPaused` or
`MusicResumed`, and sets `window_inactive`. The pause is sent only while the
device is playing. The resume is an MCI play command with no start or end
position, which by the MCI definition plays from the paused position to the end
of the disc.

## Edge cases

- The poll also restarts music that stopped for any other reason, such as a
  disc change, and it restarts the program once music is enabled again after
  level 0 (RULE-AUDIO-003).
- After a resume the music runs past the last track of the program to the end
  of the disc before the poll restarts the program.
- Without a disc or a CD device the poll sends the status and play commands on
  every tick, and the pointer is switched to the hourglass and back each time
  (RULE-AUDIO-001).

## What the sources say

None of the sources describes the repeat or the pause.

## Differences between builds

None known.

## Open questions

- How the replacement `winmm.dll` of the GOG build answers the status query and
  the resume, and so whether the resume really plays past the program there, has
  not been observed.
