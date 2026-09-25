---
id: FND-COMBAT-002
title: The Combat Results panel's page arrows, opponent portraits, force selector and exit face are fixed hit rectangles in handler 0x00451F80
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00451F80
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The `PX05012` Combat Results handler `0x00451F80` converts pointer positions
  to panel-local ones by subtracting the shared panel origin `(104,124)`.
- Its previous and next page arrows are the half-open local rectangles
  `(31,33)-(57,56)` and `(59,33)-(85,56)`.
- The five other-player portraits are `(202,16 + 36 * n)-(234,48 + 36 * n)`
  for `n` from 0 to 4. A portrait reacts only when its player has a filled
  result row.
- The viewer's force selector is one region, `(101,27)-(189,183)`, and not six
  separate portrait rectangles. A local x greater than 144 selects the right
  column, and a local y greater than 78 and greater than 130 select the second
  and third rows.
- The bottom face `(33,169)-(82,191)` leaves the panel. The handler has no
  other bottom control and no control that opens Detailed Combat.

## Interpretation

The page arrows react over 26 by 23 pixels, more than their drawn size. The
force selector picks one of six packed result slots, gutters included, while
the opponent portraits react only inside their 32 by 32 cells. Detailed Combat
(`PX05014`) is opened by a separate route from the main console, not from this
panel.

## Alternatives

None known. The pressed look of the controls was not read.

## How to reproduce

Find the function that passes the `PX05012` resource number to the image loader
and handles pointer input; read its subtraction of 104 and 124 and the
comparisons against the constants above.
