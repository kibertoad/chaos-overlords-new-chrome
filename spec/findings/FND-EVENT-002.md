---
id: FND-EVENT-002
title: The Last Turn Events handler tests only Previous, Next and one exit control, and has no Delete branch
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044F2FC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00451602
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The handler `fn_0044F2FC` loads `PX05010`, converts a pointer position inside
  the shared panel to panel-local coordinates and tests three half-open
  rectangles: Previous `(31,33)-(57,56)`, Next `(59,33)-(85,56)` and the bottom
  exit `(33,169)-(82,191)`.
- Previous on the first report and Next on the last report leave the page
  unchanged and play general-effect slot 4. A legal step calls the arrow helper
  `fn_00451602`, which plays slot 3, draws the pressed arrow and changes the
  page.
- The Enter and Execute keys activate the exit control.
- The handler has no Delete rectangle and no branch that clears a report.

## Interpretation

The panel has one 49-by-22 bottom control that closes it. Reports stay in the
table until the next resolution clears them (FND-EVENT-001); the panel cannot
delete one.

## Alternatives

None known.

## How to reproduce

Open `fn_0044F2FC` from its `PX05010` load (resource number 5010). Read the
rectangle constants built before each hit test, the virtual-key comparisons
with `0x0D` and `0x2B`, and the boundary branches that call the sound wrapper
with slot 4. Follow the legal-step branch into `fn_00451602`.
