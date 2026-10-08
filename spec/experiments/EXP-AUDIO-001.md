---
id: EXP-AUDIO-001
title: Does a game started with New Game play the turn-start sound?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and both volumes at their initialized values, effects 6 and music 5 (FND-OPTIONS-001), under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 2
fixture: EXP-AUDIO-001.json
---

## Question

In a local match started with New Game, does the play helper play the
turn-start sound, slot 9, at any turn after the first?

## Setup

As EXP-TURN-001, with the sound on (`--sound`, which sets both volumes to
their initialized values) and the objective and Mentality given explicitly, so
the run does not depend on the preferences the machine's registry holds.

## Procedure

`Rechaos.OriginalProbe new-game --scenario 4 --mentality 2 --humans 0 --seed
7272 --end-turns 4 --sound --sound-calls`.

The probe sets a breakpoint on the play helper `fn_0045851A(slot, priority)`
(FND-AUDIO-006) and records each call with the number of `roll` calls and Done
presses before it, the slot and the address of the call. The effects wrapper
calls the helper only while effects are enabled; the turn-start cue calls it
directly. The probe also reads `effects_enabled` (FND-AUDIO-002) at each call,
at each Done press and at the end of the run, and the fixture records it as
`effects_enabled`. The state is dumped at the fifth planning entry, after four
turns have begun since the first.

The run was made a second time, as `Rechaos.OriginalProbe new-game --scenario
4 --mentality 2 --time-limit 0 --humans 0 --seed 7272 --end-turns 4 --sound
--sound-calls`, with `--time-limit 0` added so the planning time limit did not
depend on the choice the previous run left, and with the probe also recording
each call of the effects wrapper `fn_00464290(slot)` in the same form, and
each call of the level setup `fn_004652A0` with the address
of the call and `effects_enabled` as it returned, the only writes of the
setting (FND-AUDIO-019). The fixture holds the second run, as `sound_calls`,
`effect_calls` and `level_setups`.

## Observations

Both runs made the same 771 calls of `roll`, the Done presses came after 328,
427, 535 and 649 of them, and they reached the same state. The helper was
called five times, each time for slot 2, the push cue, from the wrapper's call
at `0x004642AB`: once for Begin and once for each Done press. Slot 9 was never
played. `effects_enabled` was set at each read.

In the second run the wrapper was called five times, for slot 2 at Begin and at
each Done press, each passed on to the helper at the same roll. The level
setup was called once, from the title initialization at `0x00461088` before
Begin, and left `effects_enabled` set.

## Results

A test of the rebuild checks these calls, that the setting held from the title
initialization to the end and that every request to the wrapper reached the
helper, replays the match, and checks that the rebuild's local game has no
turn-start cue.

## Conclusion

The run supports RULE-AUDIO-006 for a game started with New Game: no turn
after the first begins with the sound. Network games, the only ones that play
it, were not run. With effects enabled the wrapper passed every request on, as
RULE-AUDIO-005 gives; EXP-AUDIO-002 runs the same match with effects off.
