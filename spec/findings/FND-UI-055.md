---
id: FND-UI-055
title: The title loop's first load of the title art returns to 0x004615D0, and About's load of the credits to 0x00464DFB
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004615C4..0x004615D0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00464DF2..0x00464E0C
tool: Ghidra 12.1.3
environment: null
---

## Observation

- In the title loop `fn_00460CCF` (FND-UI-009), `0x004615C4` pushes resource
  `0x82`, 130, and `0x004615C9` surface 1 for the image loader `0x00464108`,
  called at `0x004615CB`. The instruction after the call, `0x004615D0`, removes
  its four arguments. It is the first load of resource 130 in the function.
- In About's screen `fn_00464D53` (FND-UI-007), `0x00464DF2` pushes resource
  `0x64`, 100, and `0x00464DF4` surface 3 for the same loader, called at
  `0x00464DF6`. The instruction after the call, `0x00464DFB`, removes its four
  arguments, and `0x00464E0C` then calls `fn_00425EDF(0, 0, 0x1CC, 0x280)`,
  the rectangle 640 by 460 at the origin.

## Interpretation

When `0x004615D0` runs, `PX00130` is in surface 1 and the title loop has
started; when `0x00464DFB` runs, `PX00100` is in surface 3 and the credits are
about to be copied over the canvas. Which instructions copy them to the display
is not given here. Copies of the drawing area taken two seconds after a
breakpoint at each (EXP-UI-015) show the title art and the credits.

## Alternatives

None known.

## How to reproduce

In `0x00460CCF`, find the first call of `0x00464108` with the pushes `1` and
`0x82`, at `0x004615CB`, and the `add esp, 0x10` after it. In `0x00464D53`,
find the call of `0x00464108` with the pushes `3` and `0x64`, at `0x00464DF6`.
