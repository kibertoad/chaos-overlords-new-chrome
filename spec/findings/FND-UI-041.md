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
tool: original debugger probe and synchronized window BitBlt capture
environment: Windows 11, original executable in a staged installation, 640-by-460 drawing area
---

## Observation

On 2026-10-02, the hash-verified BLD-GOG-EN-1.1 executable ran scenario 0
with a 26-turn limit and seed 52421. The probe pressed Done through the match
and dismissed result panels, without changing the executable or shortening
its turn limit. The match completed after 10647 recorded random rolls.

Before Done advanced from the final city view to awards, memory held
`elapsed_turns = 25`, `match_over = 1`, the no-match-in-play flag of
FND-STATE-010 set to 1, and viewed player 0. The calendar displayed year
2050 and week 26, with executable string resource 19 at `(532,15)` in place
of the timed companion field. Its origin and the separate year/week origins
agree with FND-UI-040.

Two synchronized, non-repainting BitBlt captures agreed byte for byte; the
selection counter stayed stable and recorded marker frame 11. The top-down
32-bit BMP is retained outside the repository with SHA-256 82bb327d1b225e561ac2baa64506991885c2693a11f8ffd9bdd5d61d43062747.
The original recorded 16-bit display mode while the window DC reported
32 bits. Known Windows 11 white-block artifacts elsewhere in the city do
not affect this calendar observation.

## Interpretation

The completed-match calendar companion described by FND-UI-040 occurs in a
naturally completed match, before awards. This run supports that branch and
its first final-view drawing, not every later refresh or every scenario.

## Alternatives

The probe observes the original under a debugger and controls button presses.
It does not establish how the display behaves on older Windows versions.

## How to reproduce

Run BLD-GOG-EN-1.1 in a staged installation with its original assets, select
scenario 0 and a 26-turn limit, and seed the existing random probe with
52421. End turns and dismiss result panels until the match completes. Stop
at the final city view before pressing Done again. Read the recorded state
fields and take two synchronized window captures without PrintWindow or
scaling. Compare the calendar origins with FND-UI-040.
