---
id: RULE-AUDIO-001
title: Starting a music program
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AUDIO-001, FND-AUDIO-007, FND-PLATFORM-006, FND-EXE-004]
conflicting: []
split_with: []
related: []
---

## Summary

The game has three music programs: track 2 for the title and setup, tracks 3 to
8 in order during a game, and track 9 for the endgame. Starting a program stops
the music of any other program and plays the new one's tracks from the first
when music is enabled.

## When it runs

With mode 0 when the title screen is first entered and whenever the local game,
network game or load paths return to it; with mode 2 when the outer game and
turn function is entered and when a player's endgame screen returns to a later
human player; with mode 1 when either endgame and awards presentation starts;
and with the current `music_mode` when RULE-AUDIO-002 finds the device not
playing. It does not run when the player moves between the title and setup, opens
the Options menu or Help, or opens and closes panels during a game.

## Parameters

- `mode` (`INT32`): 0 for the title program, 1 for the endgame program, 2 for the
  game program, or -1 to keep the stored program.

## Inputs

`music_mode`, `music_enabled`.

## Procedure

```text
let first_track: INT32[3] = [2, 9, 3]
let last_track: INT32[3] = [2, 9, 8]
if mode != music_mode:
    # faded out over 32 steps of 17 ms when the CD device has a volume control
    emit MusicStopped()
    if mode != -1:
        music_mode = mode
if music_enabled != 0:
    if music_mode >= 0 and music_mode <= 2:
        # the pointer shows the hourglass while the command is sent
        emit MusicPlayRequested(first_track[music_mode], last_track[music_mode])
```

## Outputs

No return value. Sets `music_mode`. Emits `MusicStopped` when the mode changes,
then `MusicPlayRequested` with the program's first and last CD track when music
is enabled. The original asks MCI for the length of the last track and then
plays, in track, minute, second and frame positions, from the start of the first
track to the end of the last, asking for a notification that nothing uses
(FND-AUDIO-007).

## Edge cases

- Starting the program that is already selected does not stop the music first;
  it asks MCI to play the program again from its first track. This is how a
  program repeats after its last track, and a program already playing restarts
  from its first track when a caller starts it again.
- `music_mode` is -1 in the executable's data, so until the first program is
  started nothing plays. A mode of -1, while a program is stored, fades the music
  out and starts the stored program again from its first track.
- Without a disc or a CD audio device the commands fail silently
  (RULE-AUDIO-010).

## What the sources say

SRC-MANUAL-GOG, page 9, describes the Music option as a volume control for the
soundtrack and gives no track order. SRC-HELP-GOG is not known to say more.

## Differences between builds

None known.

## Open questions

None.
