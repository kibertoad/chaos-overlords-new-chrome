---
id: FND-UI-056
title: The panel-open helper keeps its travel at ebp - 4 and the width shown at ebp - 8, and copies at 0x0041965D and 0x004196DC
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041953E..0x004196F5
tool: Capstone 5.0.7
environment: null
---

## Observation

In the panel-open helper `fn_0041953E` (FND-UI-011):

- `0x00419554` to `0x0041956E` store the travel at `ebp - 4` and the source's
  left edge at `ebp - 0x14`: 320 (`0x140`) and 344 (`0x158`) when the mode
  byte at `ebp + 8` is nonzero, 344 and 0 when it is 0.
- `0x00419575` to `0x00419590` divide the startup benchmark count at
  `0x004981F8` by 4, rounding toward zero, into `ebp - 0x10`, and raise it to 1
  when it is below 1. `0x00419597` to `0x004195B2` divide the travel by it into
  the step at `ebp - 0xC`, and set the step to 16 when it is 16 or less.
- When the Slide Panels byte `0x00487840` is nonzero, `0x004195C6` calls
  `fn_00464290(0)`, the sound wrapper of FND-AUDIO-002, and `0x004195D1` sets
  the width shown at `ebp - 8` to the step. The loop at `0x004195D9` to
  `0x00419665` runs while the travel is greater than the width shown, and adds
  the step to it after each copy.
- Each pass builds two rectangles with `fn_00425EDF`: the destination
  `(124, 448 - shown, 333, 448)` and the source
  `(144, left, 353, left + shown)`, in its `(top, left, bottom, right)` order.
  The call at `0x0041965D` passes them to `fn_0042773E` with the leading
  arguments 7 and 0.
- After the loop, or at once with Slide Panels off, `0x0041966A` to
  `0x004196D8` build the same two rectangles with the whole travel in place of
  the width shown, and `0x004196DC` makes the last copy through `fn_0042773E`.
  `0x004196E4` then sets `0x004854C8` to 1 (FND-UI-051).

## Interpretation

At the copy call `0x0041965D`, `ebp - 4` holds the travel and `ebp - 8` the
width of the panel shown, so the panel's left edge is `travel - shown` pixels
right of its final place. The call at `0x004196DC` is the last copy, with the
panel in place. Each copy shows the panel's left `shown` columns, cut off at
x 448, and the loop leaves the last partial step to the final copy.

## Alternatives

None. The two arguments 7 and 0 of `fn_0042773E` are not read here.

## How to reproduce

Disassemble `0x0041953E` to `0x004196F4` and follow the stores to `ebp - 4`,
`ebp - 8`, `ebp - 0xC` and `ebp - 0x14`, the pushes before the two calls of
`fn_00425EDF` in each block, and the two calls of `fn_0042773E`.
