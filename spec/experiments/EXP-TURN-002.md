---
id: EXP-TURN-002
title: Do three turns of a new local game, each ended with no orders, draw and resolve as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, full screen, Warn if Idle Gangs and Detailed Combat switched off in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-002.json
---

## Question

As EXP-TURN-001, over three turns: do the computer players' second and third
planning passes, with gangs already on the map, and the resolutions that
follow, make the draws and reach the state the spec gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-001, pressing Done three times, each when no roll has been made
for eight seconds after `elapsed_turns` reached the previous turn's number
(`--end-turns 3`). The copy was taken when `elapsed_turns` had reached 3.

## Observations

The seed was 22250. Begin made 311 calls of `roll`, and the three turns 103,
106 and 103:

| Calls by turn | Call instruction | Bound | What FND-RNG-006 says they are |
|---|---|---|---|
| 15, 5, 5 | `0x0047172A` | 89 | the computer players' vacant offers at their planning entries, with redraws |
| 0, 2, 5 | `0x00409C24` | 8 | a pick among sectors tied for the best score in `fn_00408642` |
| 4, 5, 4 | `0x00475AC6` | 5 | the Force of each gang hired in the hire phase |
| 84, 94, 89 | `0x00475FBB` | 6 | the resolver's dice |

Turn 1 hired four gangs and turn 2 drew five offers, so one computer player
snubbed an offer in turn 1 (RULE-AI-009). The copy held 3 in `elapsed_turns`.

## Results

Every result is the one RULE-RNG-002 computes from the recorded seed.
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the three
turns as EXP-TURN-001 replays one, and the rebuild makes the same 623 calls
with the same bounds and results and reaches the same generator position and
state.

## Conclusion

Three turns agree with the spec for this seed, including the tie picks of the
computer players' sector choice and a snub.
