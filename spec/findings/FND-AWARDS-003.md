---
id: FND-AWARDS-003
title: The endgame shows a victory splash to a lone human and goes straight to the shared standings with several, whose rows have fixed positions
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042B9E0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042CE61
tool: Ghidra 12.1.3
environment: null
---

## Observation

The final awards controller `0x0042B9E0` counts the human participants. With
exactly one, it asks the renderer `0x0042CE61` for the single-player victory
presentation `DATA/PX16/PX00202`. With more than one local human it draws the
shared standings from `DATA/PX16/PX00200` and `DATA/PX16/PX00201` directly.

The shared results renderer prints each player's name unchanged, with no
place prefix, at `(197, 38 + 66 * row)`, where `row` is the displayed row. For
a ranked player it copies the 16-by-32 marker at
`(16 * (place - 1), 48 + 32 * player)` of `PX00201` to
`(113, 31 + 66 * row)`. An inactive player's row has no marker.

The statistics tab copies the opaque strip `(96,112,160,64)` of `PX00201` to
`(262, 30 + 66 * row)`. Its five values are drawn in fixed-width fields of 8,
8, 7, 6 and 6 digits, each ending at x 419, at vertical offsets 37, 46, 58, 67
and 79 from the row's top.

The Done control shares the rectangle from `(428,377)` up to but not including
`(528,425)` with the elimination card (FND-OBJECTIVE-002). The three visible
controls call the push-cue helper `0x0042CB95` (FND-AUDIO-002).

## Interpretation

A finished hot-seat game goes straight to the shared awards and statistics;
only a single human sees a victory splash first. `place` is the player's
competition standing plus one.

## Alternatives

Which of the five statistics each vertical offset holds is not recorded; the
manual lists them as Cash Earned, Cash Spent, Damage Inflicted, Casualties and
Overthrows. Whether the vertical offsets are measured from `30 + 66 * row` or
from another origin is not stated. The rectangles of the Awards and Stats
controls, the positions of the award icons, and where `PX00202` is placed are
not recorded here. Whether the controller counts local humans only, or network
humans too, is not recorded.

## How to reproduce

In `0x0042B9E0`, find the count of human participants and the branch on a
count of one that calls `0x0042CE61` for resource 202. In the
renderer, find the name output at x 197 with the 66-pixel row stride, the
marker copy with source x `16 * (place - 1)` and y `48 + 32 * player`, the
strip copy from `(96,112)` to x 262, and the five number outputs ending at
x 419.
