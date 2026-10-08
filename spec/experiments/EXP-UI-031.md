---
id: EXP-UI-031
title: At the first planning entry, does the rebuild draw every active-player marker frame and both selected-sector frames as the original does?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, full screen switched off in memory, silent, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-031.json
---

## Question

The Overlord bar animates the viewed player's marker through twelve frames
(FND-UI-038), and the city map draws the selected sector with one of two
frames picked by the pump's counter (FND-UI-017, FND-UI-048). EXP-UI-001 and
EXP-UI-003 compare the first planning entry at marker frames 5 and 6 only. Do
the other marker frames and both selection frames match the rebuild as well?

## Setup

As EXP-UI-003, with seed 52421.

## Procedure

`Rechaos.OriginalProbe new-game --scenario 0 --turns 26 --seed 52421
--end-turns 0 --white-key --order-steps <steps>`, where the steps are
`shot:SCR-UI-003` and `wait:37` in turn, thirty of each, then `extract
--experiment EXP-UI-031` over the run directory.

The state is dumped at the first planning entry, and nothing is pressed after
it. Each shot copies the drawing area as `--capture` does (docs/validation/screen-captures.md,
"Taking a capture"): two BitBlt copies from the window's device context that
must agree byte for byte while the marker counter `0x00487B90` and the pump's
counter `0x00487804` hold still, and keeps the marker frame and the pump's
counter they show. A wait of 37 ms lets the game run between shots, so the
counters move on. The bitmaps are kept with the maintainer's copy of the game
as `GAME_DIR/captures/<xxh3>`; the fixture gives each capture's xxh3 and the
xxh3 of each element of SCR-UI-003.

## Observations

Begin made 310 calls of `roll`, as in EXP-UI-001, and no shot made another.
All thirty shots kept two agreeing copies. Every one shows sector 12, column 4
of row 1, selected.

The marker frames shown were, in order, 5, 7, 8, 9, 10, 11, 0, 1, 2, 3, 3, 4,
5, 7, 8, 9, 10, 11, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 and 10, so every frame 0
to 11 appears. The pump's counter `n` was 4, 5, 6, 6, 7, 0, 0, 1, 1, 2, 2, 3,
3, 4, 5, 6, 6, 7, 7, 0, 1, 1, 2, 2, 3, 4, 4, 5, 5 and 6 at the same shots, so
the selection frame on screen, `((n + 7) % 8) / 4`, is 0 in fifteen shots
and 1 in fifteen. The two counters step at their own rates, and the pairs
seen cover frame 0 with marker frames 1 to 8 and frame 1 with marker frames
0, 1 and 7 to 11.

No element holds an exact-white pixel except the portrait of player 3, and
the whole-screen element that contains it: the one pixel EXP-UI-003 found
there.

## Results

A test of the rebuild replays the run, draws its
endpoint at each shot's marker frame and pump counter, and compares every
element of SCR-UI-003 with the shot. No element of any shot differs: outside
the masks of DEV-UI-006 (the cash row) and DEV-UI-023 (the key line) every
pixel matches, the marker in each of its twelve frames and the selected
sector's outline in both of its frames included.

## Conclusion

The run supports FND-UI-038 for the frames and the placement of the viewed
player's marker, and FND-UI-017 and FND-UI-048 for the two selection frames:
the original draws both outlines around the selected sector, and the pump's
counter picks between them as the rebuild does. It covers one viewed player
and one selected sector, and it does not measure the timers' periods.
