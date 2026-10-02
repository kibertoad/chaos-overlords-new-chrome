---
id: FND-UI-041
title: The running original draws the completed-match calendar companion in its final city view
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: dynamic
locations: []
tool: tools/Rechaos.OriginalProbe and synchronized window BitBlt capture
environment: Windows 11, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed by the probe with a 640-by-460 drawing area
---

## Observation

On 2026-10-02, the hash-verified BLD-GOG-EN-1.1 executable ran scenario 0
with a 26-turn limit and seed 52421. The probe pressed Done through the match
and dismissed result panels, without changing the executable or shortening
its turn limit. The match completed after 10647 recorded random rolls.

Before Done advanced from the final city view to awards, memory held
`elapsed_turns = 25`, `match_over = 1`, `no_match_in_play = 1`
(FND-STATE-010) and `viewed_player = 0`. The calendar displayed year
2050 and week 26, with executable string resource 19 at `(532,15)` in place
of the timed companion field. Its origin and the separate year/week origins
agree with FND-UI-040.

Two synchronized, non-repainting BitBlt captures agreed byte for byte. The
active-player marker counter `0x00487B90` of FND-UI-038 held the same value
across both, selecting marker frame 11. The top-down
32-bit BMP is retained outside the repository with SHA-256 82bb327d1b225e561ac2baa64506991885c2693a11f8ffd9bdd5d61d43062747.
The original recorded 16-bit display mode while the window DC reported
32 bits. Windows 11 drew white blocks elsewhere in the city during the run;
none overlaps the calendar row.

## Interpretation

The completed-match calendar companion described by FND-UI-040 occurs in a
naturally completed match, before awards. The captures were taken after the
result panels were dismissed, so they show the final view at that moment and
do not separate the planning-entry drawing from a later refresh. The run
covers one scenario.

## Alternatives

The probe observes the original under a debugger and controls button presses.
It does not establish how the display behaves on older Windows versions.

## How to reproduce

Run BLD-GOG-EN-1.1 staged as the environment above describes, under
`tools/Rechaos.OriginalProbe new-game` with `--scenario 0`, `--turns 26` and
`--seed 52421`, which replaces the clock value passed to `srand`. End turns
and dismiss result panels until the match completes. The probe presses Done at
the final view on its own, so halt it there before that press. Read the recorded state
fields and take two synchronized window captures without PrintWindow or
scaling. Compare the calendar origins with FND-UI-040.
