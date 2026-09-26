---
id: FND-UI-009
title: The title loop draws PX00130 full screen, and the demo promotion PX00131 has a presenter nothing calls
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00460CCF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004653DE
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The title loop in `fn_00460CCF` loads `PX00130` into surface 1 at its full
  640-by-460 size and copies it opaquely to the display before taking title
  input. It does so on first entry and on every return from local setup, from
  the network setup and from loading a game.
- `PX00131` is also a full 640-by-460 sheet. Its only renderer is the blocking
  presenter `fn_004653DE`, which draws it, redraws it after system messages, and
  ends on the event types 2, 4, 6 or 18. Nothing in the executable calls
  `fn_004653DE`.
- `PX00130` holds the game's logo and copyright notice; `PX00131` holds a
  promotion for the full game of the kind a demo version shows.

## Interpretation

`PX00130` is the title screen. `PX00131` is left over from a demo build and is
never shown by this build.

## Alternatives

- A call through a pointer table would not show up as a direct reference. No
  table holding `0x004653DE` has been found, but none has been searched for
  either.

## How to reproduce

Find the loads of resource 130 and 131 through the image loader `0x00464108`.
The first is in `0x00460CCF`; the second is in `0x004653DE`, which has no
cross-reference.
