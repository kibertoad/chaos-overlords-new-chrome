---
id: RULE-AUDIO-001
title: Starting a music program
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AUDIO-001, FND-PLATFORM-006]
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
and with the current `music_mode` when RULE-AUDIO-002 sees that playback has
ended. It does not run when the player moves between the title and setup, opens
the Options menu or Help, or opens and closes panels during a game.

## Parameters

- `mode` (`INT32`): 0 for the title program, 1 for the endgame program, 2 for the
  game program.

## Inputs

`music_mode`, `music_enabled`.

## Procedure

```text
let first_track: INT32[3] = [2, 9, 3]
let last_track: INT32[3] = [2, 9, 8]
if mode != music_mode:
    emit MusicStopped()
music_mode = mode
if music_enabled != 0:
    emit MusicPlayRequested(first_track[mode], last_track[mode])
```

## Outputs

No return value. Sets `music_mode`. Emits `MusicStopped` when the mode changes,
then `MusicPlayRequested` with the program's first and last CD track when music
is enabled. The original asks MCI for the length of the last track and then
plays from the first track to the end of the last with a notification when
playback ends.

## Edge cases

Starting the program that is already selected does not stop the music first; it
asks MCI to play the program again from its first track. This is how a program
repeats after its last track.

## What the sources say

SRC-MANUAL-GOG, page 9, describes the Music option as a volume control for the
soundtrack and gives no track order. SRC-HELP-GOG is not known to say more.

## Differences between builds

None known.

## Open questions

- What a negative mode does. No caller passes one.
- Whether starting a program that is already playing, other than after it ends,
  restarts it audibly.
