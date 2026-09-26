---
id: FND-ATTACK-002
title: The Attack picker marks the chosen opponent with a 34-by-34 frame and the chosen target with a 48-by-48 keyed overlay from PX00129
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043D073..0x0043D131
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043D93C..0x0043DAD8
tool: Ghidra 12.1.3
environment: null
---

## Observation

Both functions are called only by the Attack picker `fn_0043B290`, each from
eleven call sites, always as a pair after the picker has copied part of its
panel from surface 7 back to the screen. Ranges are in FND-EXE-004. Surface 6
holds `PX00129` (FND-UI-031); the copy wrapper `fn_00427864` with mode 1 keys
out exact white (FND-PLATFORM-008). Positions are screen coordinates; the
panel's top-left corner is `(104,124)`, the origin the picker subtracts
before its own rectangle tests.

- `fn_0043D93C(opponent)` does nothing for -1. For `n` 0 to 4 it copies the
  34-by-34 cell `(120,171)` of surface 6 with mode 1 to the screen at
  `(201, 139 + 36 * n)`. The picker's opponent rectangles are local
  `(98, 16 + 36 * n)-(130, 48 + 36 * n)`, screen `(202, 140 + 36 * n)`, so the
  frame lies one pixel outside the 32-by-32 portrait cell on every side.
- `fn_0043D073(target_cell)` does nothing for -1. For a cell `c` it copies the
  48-by-48 cell `(66,299)` of surface 6 with mode 1 to the screen at
  `(247 + 68 * (c % 3), 142 + 90 * (c / 3))`. The picker's target cells start
  at local x 135, 202 and 270 and local y 16 and 105 (FND-ATTACK-001), so the
  overlay sits 8 pixels right of and 2 pixels below each cell's corner.
- The picker's opponent handler (`0x0043C098`..`0x0043C27F`) sets the new
  opponent, resets the target cell to -1, restores the frame column, screen
  `(201,139)-(235,317)`, from surface 7 `(97,159)`, calls
  `fn_0043D93C`, and rebuilds the target list. Its target handler
  (`0x0043C298`..`0x0043C496`) restores the previous target's 48-by-48 area
  from surface 7 before it calls `fn_0043D073` for the new cell.

## Interpretation

The chosen opponent is shown by a frame around its portrait and the chosen
target by a keyed marker drawn over the upper left of its card. The marker's
art is whatever `PX00129` holds at `(66,299)`; the manual's description of a
marker on the right of the card does not match the drawn position.

## Alternatives

What the 48-by-48 cell at `(66,299)` of `PX00129` looks like has not been
checked against the image.

## How to reproduce

In `0x0043D93C`, find the switch on the argument with the five destination
rectangles of left `0xC9` and the source rectangle `(0xAB,0x78,0xCD,0x9A)`
passed to `0x00427864` with surfaces 6 and 0 and mode 1. In `0x0043D073`, find
the constants `0x5A`, `0x8E`, `0xF7` and the multiply by 68, and the source
rectangle `(299,0x42,0x15B,0x72)`.
