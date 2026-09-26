---
id: FND-UI-011
title: Panels slide in from and out to the right edge in steps set by a startup copy benchmark
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041953E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004196F5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00425EDF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00432954
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The panel-open helper `fn_0041953E` and the matching close helper
  `fn_004196F5` copy a 344-by-209 panel sideways from or toward the right edge
  of the screen. The rectangle helper `fn_00425EDF` packs its arguments as
  `(top, left, bottom, right)`, which the `BitBlt` call in `fn_0042773E`
  confirms.
- In the primary form the panel travels 344 pixels from backing-buffer x 0 into
  screen x 104 to 448. Its final rectangle has corners `(104,124)-(448,333)`.
- In the alternate form the helpers take a 320-pixel source starting at backing
  x 344 and move it to the same right-edge position, ending at corners
  `(128,124)-(448,333)`. Six caller pairs pass a nonzero mode for this form:
  Item Information `fn_0044B699` (`PX05001`), Site Information `fn_0044C476`
  (`PX05002`), City and Sector Financial `fn_0044D1BB` (`PX05008`,
  `PX05019`), the Hire comparison `fn_004546C5` (`PX05016`), Game Information
  `fn_0045519D` (`PX05021`) and Gang Definition Information `fn_00455B6B`
  (`PX05022`). The other seventeen caller pairs pass 0.
- The Hire handler loads its 344-by-209 image to backing
  `(top=144, left=344, bottom=353, right=688)` and then uses the alternate
  form, so only backing x 344 to 664 reaches the screen, at x 128 to 448.
- Both helpers take their step from the startup benchmark `fn_00432954`, which
  counts identical screen copies for just over one second. A helper divides the
  count by four, divides its travel by the result, and raises the step to 16
  pixels when it is smaller.
- With Slide Panels on (byte `0x00487840`), the open helper plays slot 0 before
  it starts and the close helper plays slot 1 (FND-AUDIO-002). With Slide
  Panels off both skip the intermediate copies and the sound and show only the
  final state.

## Interpretation

A sliding panel takes about a quarter of a second on any machine, since the
travel is split into about a quarter of the copies the machine can make in a
second. On a slow machine the 16-pixel minimum step shortens the slide further.
The slide blocks input while it runs.

## Alternatives

- How the last partial step is handled, and whether the panel slides as a whole
  or is revealed from its left edge, has not been recorded.
- What happens when the benchmark count is below four, which would make the
  divisor 0, has not been read.

## How to reproduce

Open `0x0041953E` and `0x004196F5`, find the division of the benchmark global
by 4, the division of the travel by it, and the compare with 16. List their
callers and the constant mode each passes.
