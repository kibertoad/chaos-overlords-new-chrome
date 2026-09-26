---
id: FND-SETUP-012
title: The running original opens its full local setup with Kill 'Em All selected when no scenario preference is stored
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

On 2026-09-13 the original executable of BLD-GOG-EN-1.1 was started and taken
to its full local setup screen. The first scenario button's selection light
was the only lit scenario light, and the description panel named the same
objective, Kill 'Em All. Five copies of the window taken 120 ms apart were
identical byte for byte. The capture is a PNG file with xxh3
`a83f82a2aab84d9a1e0e9de626409149` (SHA-256
`ab81528fa770e991140d5133c5dbb092751c7c3f723195cb33dcb41740410275`), kept
with the maintainer's copy of the game as
`GAME_DIR/captures/a83f82a2aab84d9a1e0e9de626409149.png`.

Before the run, searches of the current user's and the machine's registry
hives found no `prefsObjective` value, so no stored preference chose the
scenario.

## Interpretation

With no stored preference, a fresh setup selects the first scenario button,
Kill 'Em All. Together with FND-SETUP-009 this makes the value 0 of the
scenario global Kill 'Em All.

## Alternatives

The capture was taken from the desktop, not at the 640x480 canvas with no
scaling, so it cannot serve as a pixel reference for the screen. Whether the
registry search covered every place the preference loader reads is not
recorded.

## How to reproduce

Remove any `prefsObjective` value from the game's registry key, start
`Chaos Overlords.exe`, choose a new local game from the title screen, and
note which scenario light is lit and which objective the description panel
names.
