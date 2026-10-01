---
id: RULE-AUDIO-002
title: Music restarts a stopped program and sends pause and resume requests on focus changes
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AUDIO-014, FND-AUDIO-001, FND-AUDIO-007, FND-UI-023, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-AUDIO-011, RULE-AUDIO-001, RULE-AUDIO-003, RULE-UI-008]
---

## Summary

About six times a second the game asks MCI whether the CD is playing. When it
is not, music is enabled and the window is active, the current program is
requested again from its first track. On focus loss the executable requests
a pause; on focus regain it reapplies levels and requests playback with no
start or end position. The end-of-playback MCI notification changes nothing.

The shipped GOG wrapper rejects the pause command and ignores the resume
request without a start position (RULE-AUDIO-011). These are executable
requests, not proof that this build audibly pauses and resumes.

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
- On a device implementing the MCI resume, music can run past the last track of the program to the end
  of the disc before the poll restarts the program.
- Without a disc or a CD device the poll sends the status and play commands on
  every tick, and the pointer is switched to the hourglass and back each time
  (RULE-AUDIO-001).

## What the sources say

None of the sources describes the repeat or the pause.

## Differences between builds

None known.

## Open questions

- Audible focus behavior of the GOG wrapper has not been recorded dynamically;
  its static pause and no-start play paths are recorded in RULE-AUDIO-011.
