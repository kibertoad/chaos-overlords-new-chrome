---
id: EXP-UI-035
title: What does the planning clock bar show at the next planning entry?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-035.json
---

## Question

When planning ends, the bar is left as last drawn (RULE-TIMER-002). In a match
of two local humans with a time limit, what does the bar's rectangle show when
the next human's clock starts: the bar the previous turn left, or something the
planning entry drew?

## Setup

As EXP-TURN-001.

## Procedure

`Rechaos.OriginalProbe new-game --humans 0,1 --seed 3590 --time-limit 1
--end-turns 2 --delays 1:14000 --expire-turns 2 --clock-captures`.

The limit is 30 seconds. In turn 1 player 0 presses Done 14 seconds into its
turn, and player 1 presses Ready and Done at once. In turn 2 player 0 presses
no Done and the turn runs out, and player 1 again presses Ready and Done.

With `--clock-captures` the probe breaks at `0x0041B8C8` in the start helper
of the planning clock, after it has stored `planning_start_ms` and before it
draws the bar (FND-TIMER-003), and copies the drawing area there. It records
the player, `elapsed_turns`, and the width and elapsed time of the last bar
drawn before the copy, read at `0x0041B96D`.

## Observations

The run made 467 calls of `roll`. Four clocks started:

| Player | `elapsed_turns` | Last bar before the start |
|---|---|---|
| 0 | 0 | None |
| 1 | 0 | Width 33, at 13682 ms of player 0's turn |
| 0 | 1 | Width 60, at 42 ms of player 1's turn |
| 1 | 1 | Width 2, at 29164 ms of player 0's turn, which ran out |

In all four copies the bar's rectangle `(520, 336, 60, 3)` has the same digest:
the full bar of the console's art, the same pixels as at the untimed planning
entry of EXP-UI-001. Each copy shows the city screen with its console, drawn
by the planning entry before the clock starts.

Turn 2's clock redrew the bar at the recorded times with the widths and
warnings of RULE-TIMER-003 and ran out after 30003 ms. The first bar of each
turn was drawn 6 to 10 ms after the stored start, the time the copy took.

## Results

`EveryPlanningEntryPutsBackTheConsolesBar` in
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.Timer.cs` checks that every
copy holds the console's own bar, the digest the rebuild draws at the endpoint
of EXP-UI-001. `ThePlanningEntryPutsBackTheConsolesBar` in
`tests/Rechaos.Tests/PlanningTimerLoopTests.cs` checks that the rebuild's
planning entry forgets the bar of the turn before, so its entry panels show the
console's bar. `ThePlanningClockMatchesTheOriginals` replays turn 2's clock,
and `TheRebuildStartsTheSameMatch` the whole run.

## Conclusion

The run supports RULE-TIMER-002: the bar is left as last drawn when planning
ends, and the next planning entry redraws the console, which puts back the
full bar of its art before the clock starts.
