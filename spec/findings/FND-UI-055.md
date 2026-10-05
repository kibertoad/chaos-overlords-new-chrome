---
id: FND-UI-055
title: The title loop's first load of the title art returns to 0x004615D0
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
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the title loop `fn_00460CCF` (FND-UI-009), `0x004615C4` pushes resource
`0x82`, 130, and `0x004615C9` surface 1 for the image loader `0x00464108`,
called at `0x004615CB`. The instruction after the call, `0x004615D0`, removes
its four arguments. It is the first load of resource 130 in the function.

## Interpretation

When `0x004615D0` runs, `PX00130` is in surface 1 and the title loop has
started. Which instruction copies it to the display is not given here; a copy
of the drawing area taken two seconds after a breakpoint there (EXP-UI-015)
shows the title art.

## Alternatives

None known.

## How to reproduce

In `0x00460CCF`, find the first call of `0x00464108` with the pushes `1` and
`0x82`, at `0x004615CB`, and the `add esp, 0x10` after it.
