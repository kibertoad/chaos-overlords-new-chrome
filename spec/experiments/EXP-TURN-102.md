---
id: EXP-TURN-102
title: Does the planning clock run while the menu bar is open?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 3
fixture: EXP-TURN-102.json
---

## Question

While the original's menu bar is open during a timed turn, is the clock bar
redrawn, does the redraw countdown of RULE-TIMER-003 go on counting the ticks,
and does the elapsed time of RULE-TIMER-002 go on running? Can a turn run out
in the menu?

## Setup

As EXP-TURN-001.

## Procedure

`Rechaos.OriginalProbe new-game --seed <seed> --time-limit 1 --end-turns 2
--expire-turns 1,2 --menu 1:5000:12000,2:5000:35000 --clock-captures`, once
each with seeds 3561, 3562 and 3563.

The limit is 30 seconds and no turn presses Done. Five seconds into each turn,
measured as `timeGetTime` less `planning_start_ms` (FND-TIMER-003), the probe
posts `WM_SYSCOMMAND` with `SC_KEYMENU` to the game window, which opens the menu
bar as the Alt key does. It polls `GetGUIThreadInfo` until the game's thread
reports menu mode, holds the menu open until 12 seconds (turn 1) or 35 seconds
(turn 2) after the posting, posts Escape until menu mode ends, and records the
four times. The run fails when menu mode ends before the probe closes it.

In the menu turns the probe also records every call of the presentation timer's
callback `fn_004327C0` for slot 0 (FND-TIMER-002), from the start of the clock
to its expiry, besides the redraws of the bar at `0x0041B96D` and the expiry
tests that RULE-TIMER-002 runs, as in EXP-TURN-046.

## Observations

The runs made 510, 494 and 506 calls of `roll`. In every turn the thread was in
menu mode, with `GUI_INMENUMODE` set, from 10 to 61 ms after the posting until
Escape. The callback of slot 0 went on firing while the menu was open, about
every 166 ms.

Turn 1 of each run:

| Seed | Menu open | Menu closed | Last bar before | First bar after | Expiry |
|---|---|---|---|---|---|
| 3561 | 5026 ms | 17055 ms | 4715 ms, width 51 | 17659 ms, width 26 | 30002 ms |
| 3562 | 5064 ms | 17064 ms | 4815 ms, width 51 | 17598 ms, width 26 | 30006 ms |
| 3563 | 5044 ms | 17005 ms | 4753 ms, width 51 | 17539 ms, width 26 | 30001 ms |

No bar was drawn and no warning sounded while the menu was open. In each run
one tick of slot 0 fell between the last bar and the posting of the menu key,
so the countdown stood at 5 when the menu opened, and the first bar after the
close came on the fourth tick after it. The bars after the close have the
width of the whole elapsed time, menu included, and the turn ran out at 30
seconds as an untouched turn does.

In turn 2 the menu was held past the limit. The last expiry test before the
menu came at 5015, 5015 and 5007 ms. The turn ended at the first test after the
close, at 40027, 40068 and 40068 ms, with no bar drawn after the close.

The copies of `--clock-captures` at each clock's start all hold the console's
own full bar, as in EXP-UI-035.

## Results

`ThePlanningClockMatchesTheOriginals` in
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.Timer.cs` replays each menu
turn tick by tick through the recorded ticks of slot 0, with the rebuild's
PlanningTimer paused from the posting to the close. It redraws on exactly the
original's ticks with the original's widths and warnings, and expires where the
original did. `TheGameMenuKeepsOneTickOfTheRedrawCountdown` and
`TheElapsedTimeRunsOnInTheGameMenu` in
`tests/Rechaos.Tests/PlanningTimerPolicyTests.cs` check the same two
behaviours on their own. `TheRebuildStartsTheSameMatch` replays the runs.

## Conclusion

The runs support RULE-TIMER-003: the menu bar keeps the event pump from
running, so the bar is not redrawn while it is open, and the timer flag keeps
one tick for the first pass after the close. They support RULE-TIMER-002: the
elapsed time runs on in the menu, so a turn can pass its limit there and ends
at the first test after the menu closes.
