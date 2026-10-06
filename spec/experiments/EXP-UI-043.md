---
id: EXP-UI-043
title: What do the city console's tiles and the sector view's back control show while the right button is held on them?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-043.json
---

## Question

At the planning entry of turn 7 of the match of EXP-TURN-071, what does the
original draw while the right button is held on the Ranking tile of the city
screen and of the sector view, and on the sector view's back control?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-071, with `--white-key`, and after the dump `--order-steps
exit,exit,rdown:576:240,shot:SCR-UI-003,rup:300:300,open:19,rdown:576:240,shot:SCR-UI-004,rup:300:300,rdown:20:425,shot:SCR-UI-003,rup:20:425`.
`rdown:x:y` writes both pointer points of FND-UI-020 and posts
`WM_RBUTTONDOWN`, and `rup:x:y` writes them and posts `WM_RBUTTONUP`; a shot
taken while the button is down writes the held point again as in EXP-UI-041.

## Observations

The run made the same 1182 calls of `roll` with the same bounds and results
as EXP-TURN-071. All three shots were kept, with every light byte 0:

| Step | Screens | Marker frame | Pump counter | Frame counter | Selected sector |
|---|---|---|---|---|---|
| 3 | SCR-UI-003 | 7 | 1 | 1 | 12 |
| 7 | SCR-UI-004 | 7 | 4 | 4 | 19 |
| 10 | SCR-UI-003 | 3 | 1 | 1 | 19 |

- With the right button held on the Ranking tile, the city screen's tile is
  drawn pressed, with the same pixels as the left-button press of EXP-UI-041,
  and the sector view's tile is drawn pressed as well. No panel had opened
  when the button came up off the tile.
- A right press on the back control left the sector view at once: after the
  press, before the release, the probe found the city screen, and the shot
  shows the city map with sector 19 selected.

## Results

`TheOrderMenusGiveTheOriginalsOrders` in
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.Orders.cs` follows the
right press on the back control back to the city. The rebuild takes a right
press on the city and sector screens as a cancel, so `TheRebuildDrawsWhatTheOriginalDrew` skips
the three captures until the rebuild presses the tiles with the right button
(issue #525).

## Conclusion

The run shows that the right button holds a console tile as the left one does
and that a right press on the back control acts at the press, before the
release. The static reading of the right button's event types in pull request
#466 says the same; this entry is to be cited from it once it is merged.
