---
id: FND-INFLUENCE-001
title: The Influence picker selects an unfinished site through three fixed rectangles and opens its details on double-click
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043F692
tool: Ghidra 12.1.3
environment: null
---

## Observation

`0x0043F692` is the handler of the `PX05005` Influence picker and the only
code that requests resource number 5005. It tests the pointer, in the shared
panel's own coordinates, against three half-open rectangles written
`(x1,y1)-(x2,y2)`:

| Site slot | Rectangle |
|---|---|
| 0 | `(106,17)-(226,81)` |
| 1 | `(208,73)-(328,137)` |
| 2 | `(106,127)-(226,191)` |

Each test is guarded by the site's state: for each of the sector's three site
slots, selection is enabled only when the Resistance of the site's definition
differs from the slot's progress. A single click on an enabled slot selects
it. Cancel closes the picker without an order. Confirmation is refused while
no slot is selected. The double-click branch uses the same three guarded
rectangles and opens the details of the site clicked.

## Interpretation

Only a site that is not yet complete can be chosen for Influence, and a
completed site does nothing on either a click or a double-click. The input
rectangles are not the same as the staggered site pictures the panel draws,
so the drawn art does not define where a click lands.

## Alternatives

The panel's origin on the screen is not given by this finding, so the
rectangles are recorded in panel coordinates only.

## How to reproduce

Find the call that loads resource 5005; it is in `0x0043F692`. The three
rectangle tests are the comparisons against 106, 208 and 106 for the left
edges.
