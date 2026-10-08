---
id: EXP-AUDIO-002
title: With sound effects off, does a game started with New Game play the turn-start sound, and does the effects wrapper pass any request on?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-AUDIO-002.json
---

## Question

EXP-AUDIO-001 ran with sound effects enabled. With them off, does the play
helper still play nothing but what the effects wrapper passes on, so no
turn-start sound, slot 9, at any turn after the first? Does the wrapper pass
on none of the requests it gets, and do effects stay off for the whole run?

## Setup

As EXP-TURN-001: the probe writes 0 into both sound levels once the
preferences are loaded, before the title initialization's level setup derives
`effects_enabled` from them (FND-AUDIO-019). The objective, Mentality and
planning time limit are given explicitly, so the run does not depend on the
choices an earlier run left.

## Procedure

`Rechaos.OriginalProbe new-game --scenario 4 --mentality 2 --time-limit 0
--humans 0 --seed 7272 --end-turns 4 --sound-calls`, the settings of
EXP-AUDIO-001 without `--sound`.

The probe records each call of the play helper `fn_0045851A(slot, priority)`
(FND-AUDIO-006) and of the effects wrapper `fn_00464290(slot)` (FND-AUDIO-002)
with the number of `roll` calls and Done presses before it, the slot and the
address of the call, and each call of the level setup `fn_004652A0` with the
address of the call and `effects_enabled` as it returned, which are the only
writes of the setting (FND-AUDIO-019). It also reads `effects_enabled` at each
Done press and at the end of the run. The fixture holds them as `sound_calls`,
`effect_calls`, `level_setups` and `effects_enabled`. The state is dumped at
the fifth planning entry, after four turns have begun since the first.

## Observations

The run made 771 calls of `roll`, and the Done presses came after 328, 427,
535 and 649 of them, the same calls and results as EXP-AUDIO-001, and it
reached the same state. The level setup was called once, from the title
initialization at `0x00461088` before Begin, and left `effects_enabled`
clear; every other read found it clear as well. The wrapper was called five
times, each for slot 2, the push cue: once at Begin and once at each Done
press, at the same rolls as in EXP-AUDIO-001. The play helper was never
called.

## Results

A test of the rebuild checks that the setting stayed off from the title initialization to the end, that the wrapper
was asked for the push cue at Begin and at each Done press and passed none of
them on, that the helper never played the turn-start cue, replays the match,
and checks that the rebuild's local game has no turn-start cue.

## Conclusion

The run supports RULE-AUDIO-006 for a game started with New Game with effects
off: the helper is not called at all, so the turn-start sound, which bypasses
the effects setting, does not play either. It supports RULE-AUDIO-005 for the
wrapper's test: with `effects_enabled` clear, the wrapper passed none of its
requests on. The settings do not change the rolls or the state.
