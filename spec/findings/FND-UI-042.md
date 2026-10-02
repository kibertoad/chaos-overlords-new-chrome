---
id: FND-UI-042
title: The seeded completed match enters its final city without an open report panel
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: dynamic
locations: []
tool: original debugger probe with panel entry and return breakpoints and paired BitBlt capture
environment: Windows 11, staged original installation, 640-by-460 drawing area
---

## Observation

On 2026-10-02, the hash-verified BLD-GOG-EN-1.1 executable repeated the
scenario 0, 26-turn, seed 52421 run of FND-UI-041 and EXP-TURN-041. Ordinary
turn-entry panels were dismissed before each Done press. After the match
ended, the probe stopped before dismissing any final-entry panel or pressing
Done again. Entry and return breakpoints tracked Combat Results and Last
Turn Events throughout the run.

At that stop the open-panel count was 0, elapsed turns was 25 and the
recorded random-call count was 10647. The final city was visible directly.
No panel dismissal at final entry was needed to reach it. Two synchronized
BitBlt reads agreed byte for byte, with marker frame 6. Their top-down
32-bit BMP SHA-256 is
98845a00a9ee2363181e690b267a8736e171b9f10bc997a788882ebebb770901;
the captures and memory remain outside the repository. The known Windows 11
white-block artifacts do not change the tracked panel count.

## Interpretation

This particular final entry has no automatic report panel to dismiss. It
supports the direct-city branch for the recorded endpoint and rules out
the possibility that FND-UI-041 reached that city only by dismissing a
final-entry report. FND-UI-039 still supports ordinary presentation work
during final visits; this run does not demonstrate its report-bearing path.

## Alternatives

An open-panel count does not establish that all players or other seeds have
no reports. It does not measure the contents of report storage or establish
the ordering of a final visit that does open reports. Debugger pauses and
controlled input do not reproduce uninterrupted input timing.

## How to reproduce

Repeat FND-UI-041 with panel entry and return tracking enabled. Dismiss
ordinary planning-entry reports before Done. At match completion, stop
before the final-entry dismissal call, record the open-panel count and
elapsed turns, and capture the window twice without repainting. Verify
the 10647-call endpoint and that awards have not been entered.
