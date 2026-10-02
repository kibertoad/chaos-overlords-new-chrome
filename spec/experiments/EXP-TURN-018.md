---
id: EXP-TURN-018
title: Does a new local Kill 'Em All game in which the human's gang never hides draw and resolve as the spec gives, up to the human's elimination?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-018.json
---

## Question

In the earlier runs the human's gang hides, and no gang of family 2, 3 or 5
attacks. With the human's gang left without orders, so that the computer
players can see it, do the turns draw and reach the state the spec gives?

## Setup

As EXP-TURN-001.

## Procedure

1. As EXP-TURN-004, pressing Done twenty-five times with no orders
   (`--end-turns 25`), three times, each with its own seed.
2. For the run whose `roll` calls include the family-2 attack target draw at
   `0x0042047A`, repeat it with its seed over twenty-four turns, then over
   twenty-three (`--seed 36429 --end-turns 23`).

## Observations

Of the three runs, only the one with seed 36429 made calls at
`0x0042047A`: call 8165 in turn 21 and call 9752 in turn 23, each with bound
1. In it and in the repetition over twenty-four turns the twenty-fourth Done
press reached no planning phase, and the probe stopped with no state copied.
The repetition over twenty-three turns made 10629 calls, 318 before the first
Done press, and copied the state after the twenty-third turn. In it player 0's
`controller` was -2.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run up to
the planning phase that follows the human's elimination. The rebuild makes the
same calls with the same bounds and results and reaches the same generator
position and state.

## Conclusion

The run agrees with the spec over twenty-three turns of Kill 'Em All with a
visible human gang, including the attacks of family-2 gangs (RULE-AI-021).
