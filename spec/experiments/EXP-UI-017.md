---
id: EXP-UI-017
title: Does the endgame look the same in the rebuild?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory until the steps after the dump, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-017.json
---

## Question

When a match ends, does the endgame draw the same pixels in the rebuild as in
the original, on the Awards tab and on the Stats tab?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-058 (`--scenario 8 --mentality 1 --end-turns 30 --seed 8101`),
with `--white-key --order-steps
shot:SCR-AWARDS-001,strip:504:57:0,shot:SCR-AWARDS-001,strip:452:57:0,shot:SCR-AWARDS-001`.
The match ends before the thirty Done presses run out, and the state is dumped
once the endgame has drawn (FND-AWARDS-005). The steps copy the endgame, press
Stats, copy it, press Awards and copy it again.

## Observations

The run made the same 4662 calls of `roll` with the same bounds and results as
EXP-TURN-058, and the endgame ranks the same players. The three shots were
kept, the first and third on the Awards tab and the second on the Stats tab.

Around the frame the screen is black. Each row has a fill 20 pixels wide in
its player's colour behind the place marker, the name in the plain font, and,
on ranked rows, the score caption with the score right-aligned under it. On the
Awards tab the award icons have no background of their own. Two rows share
place 5.

## Results

`TheRebuildDrawsWhatTheOriginalDrew` in
`tests/Rechaos.Tests/ScreenCaptureTests.cs` compares the three captures, with
the rebuild entering the endgame from the replayed save of the decided match
and the tab presses replayed as reference clicks. Every element matches.
`TheRebuildStartsTheSameMatch` in
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the 4662
rolls.

A first comparison found the rebuild drawing the endgame translucent over an
empty console, narrower fills, the names in the players' colours, no score
caption or score, the award icons 50 pixels apart in their source with a
white background, no awards strip behind them, and a tab light in place of the
tab mark.

## Conclusion

The run supports SCR-AWARDS-001 on both tabs for a match that ended with
several players active, and FND-AWARDS-004 for the rows' fills, names, score
caption and score, the keyed award icons and the tab mark.
