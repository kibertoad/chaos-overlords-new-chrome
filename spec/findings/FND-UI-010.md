---
id: FND-UI-010
title: A capture of the running original shows the combat panel's 64-by-64 gang portraits and its two beveled Force tracks
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: dynamic
locations: []
tool: window capture of the original running
environment: Windows 11, original executable run directly
---

## Observation

A window capture of the Detailed Combat panel of the original shows:

- the two gangs' 64-by-64 portraits at panel-local `(150,48)` and `(223,48)`,
  the panel's top-left corner being screen `(104,124)`;
- under them, two 60-by-3 Force tracks at panel-local y 114 and y 121;
- each track drawn as three rows of different intensity, light on top, full in
  the middle and dark at the bottom, not as a flat fill.

The capture's hash and the capture settings (scaling, filtering) were not
recorded.

## Interpretation

The gang portraits of Detailed Combat are drawn at their native 64-by-64 size,
and its Force bars are three-row bevels like the meters of the detailed sector
screen (FND-UI-036).

## Alternatives

None known.

## How to reproduce

Start a game in the original, give one gang an Attack order against a gang in
the same sector, end the turn with Detailed Combat enabled in the Options menu,
and capture the window at 640 by 460 without scaling while the combat panel is
on screen. Measure the portrait and track rectangles from the panel's top-left
corner at `(104,124)`.
