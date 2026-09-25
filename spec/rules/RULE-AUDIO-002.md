---
id: RULE-AUDIO-002
title: Music repeats its program when it ends and pauses while the window is inactive
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AUDIO-001]
conflicting: []
split_with: []
related: [RULE-AUDIO-001]
---

## Summary

When the last track of a program finishes, the program starts again from its
first track. When the game's window loses focus the music pauses, and it
resumes where it stopped when the window regains focus.

## When it runs

In the main event pump, when it receives MCI's notification that playback has
ended, or the window's deactivation or activation.

## Parameters

- `message` (`INT32`): 0 for the end of playback, 1 for the window becoming
  inactive, 2 for the window becoming active.

## Inputs

`music_mode`.

## Procedure

```text
if message == 0:
    call RULE-AUDIO-001(music_mode)
else if message == 1:
    emit MusicPaused()
else if message == 2:
    emit MusicResumed()
```

## Outputs

No return value. Starts the current program again, or emits `MusicPaused` or
`MusicResumed`.

## Edge cases

None known.

## What the sources say

None of the sources describes the repeat or the pause.

## Differences between builds

None known.

## Open questions

- Whether the pause and resume calls are made while music is disabled, and
  what MCI does with them then.
- At shutdown the game closes the MCI device; the order of that against its
  other shutdown work is not recorded.
