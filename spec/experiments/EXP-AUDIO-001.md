---
id: EXP-AUDIO-001
title: Does a game started with New Game play the turn-start sound?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels as the preferences file holds them, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-AUDIO-001.json
---

## Question

In a local match started with New Game, does the play helper play the
turn-start sound, slot 9, at any turn after the first?

## Setup

As EXP-TURN-001, with the sound left on (`--sound`).

## Procedure

`Rechaos.OriginalProbe new-game --humans 0 --seed 7272 --end-turns 4 --sound
--sound-calls`.

The probe sets a breakpoint on the play helper `fn_0045851A(slot, priority)`
(FND-AUDIO-006) and records each call with the number of `roll` calls and Done
presses before it, the slot and the address of the call. The effects wrapper
calls the helper only while effects are enabled; the turn-start cue calls it
directly. The state is dumped at the fifth planning entry, after four turns
have begun since the first.

## Observations

The run made 771 calls of `roll`, and the Done presses came after 328, 427,
535 and 649 of them. The helper was called five times, each time for slot 2,
the push cue, through the effects wrapper: once for Begin and once for each
Done press. Slot 9 was never played.

## Results

`ANewGameHasNoTurnStartSound` in
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.Sounds.cs` checks these
calls, replays the match, and checks that the rebuild's local game has no
turn-start cue.

## Conclusion

The run supports RULE-AUDIO-006 for a game started with New Game: no turn
after the first begins with the sound. Network games, the only ones that play
it, were not run.
