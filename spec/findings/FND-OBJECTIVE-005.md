---
id: FND-OBJECTIVE-005
title: The Player Ranking panel places each portrait by its score scaled into 140 pixels, and closes on its button or on Enter or plus
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004518D9..0x00451F7F
tool: Ghidra 12.1.3
environment: null
---

## Observation

Rectangles are written `(x, y, width, height)`.

The panel presenter `0x004518D9`, called only from `0x004718EE`, loads
resource `0x1393` (5011) into surface 7 at `(0, 144, 344, 209)`. Over the
scores at `0x004A2790` that are not -32000 it finds the minimum `lo` (starting
from 100,000) and the maximum `hi` (starting from -100,000), and sets `range =
hi - lo + 1` and a single-precision factor `140.0 / range`. When `range` is 1
every slot's offset is 70; otherwise slot `p`'s offset is `(hi - score[p]) *
factor` with its fraction removed toward zero (`0x00449BD2`, then `__ftol`).

For each slot whose standing byte at `0x004ABC08` is not 0xFF, it copies the
portrait, source `(32 * portrait, 480, 32, 32)` of surface 6, unscaled to
`(98 + 40 * p, 162 + offset, 32, 32)` of surface 7, which is `(98 + 40 * p, 18 +
offset)` from the panel's top-left. It then slides the panel in (`0x0041953E`).

Input, in its own event loop:

- A key event whose code is `0x2B` or `0x0D` draws the button at
  `(137, 293, 50, 23)` pressed through `0x00418CCC` and closes.
- A button press outside `(104, 124, 344, 209)` calls `0x00464290` with 4.
  Inside, the point is made panel-relative; inside `(33, 169, 49, 22)` it
  runs the held-button helper `0x00418821` on the screen rectangle
  `(137, 293, 50, 23)` and closes when that reports a release inside. Other
  presses inside the panel do nothing.
- A repaint event copies the background and the panel `(0, 144, 344, 209)` of
  surface 7 back to `(104, 124)`.

On closing it slides the panel out (`0x004196F5`), calls `0x004120CB` and sets
`0x00498100` to 1.

## Interpretation

The height of a portrait on its rail follows the score, not the standing: the
leader's portrait sits at the top of its rail (offset 0) and the others lower
in proportion to how far their score is behind, over at most 140 pixels. Tied
players share a height. When every active player has the same score, all
portraits sit at offset 70. The x value is the portrait's left edge. The panel
closes on its button, on Enter, or on the plus key.

## Alternatives

Surface 6 is taken to be the interface sheet `DATA/PX16/PX00129`, as in the
other portrait copies. Whether key code `0x2B` is the plus key or another key
with that code is not recorded.

## How to reproduce

Open `0x004518D9` in Ghidra: the min and max loop at `0x0045194A`, the offset
loop at `0x004519EC`, the portrait copies at `0x00451A6E`, and the event
switch at `0x00451F2A`.
