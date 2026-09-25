---
id: FND-UI-005
title: Site Information uses the 320-pixel alternate panel and draws every number with the modifier helper
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044C476
tool: Ghidra 12.1.3
environment: null
---

## Observation

The Site Information handler `fn_0044C476` loads `PX05002` into surface 7 at
backing corners `(344,144)-(688,353)` and slides it in through the 320-pixel
alternate crop, ending at screen `(128,124)-(448,333)`. The site portrait is
copied to backing `(372,159)-(492,223)`, screen `(156,139)-(276,203)`. The site
name is written at backing `(504,171)`, screen `(288,151)`. Resistance,
Tolerance, Support and Cash are two-cell numbers at backing x 612, screen x 396,
on screen rows 169, 187, 196 and 205. The fourteen statistics are at screen x
300 and 396 on rows 244, 253, 271, 280, 289, 298 and 307. The only close face is
the alternate panel's `(161,293)-(210,315)`.

## Interpretation

Site Information shifts every field right into the alternate crop, like Item
Information, and draws its signed values with the fixed two-cell modifier
helper (FND-UI-006).

## Alternatives

- Whether Resistance is the remaining Resistance of the site in the sector or
  the definition's base value has not been read here.

## How to reproduce

Find the load of resource 5002 in `0x0044C476` and the constants 372, 159, 504
and 612 among its drawing calls.
