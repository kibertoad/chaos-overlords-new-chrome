---
id: FND-FINANCE-001
title: The Financial panel is drawn as the 320-pixel alternate panel with four-cell value fields and its own close control
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044D1BB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00414187
tool: Ghidra 12.1.3
environment: null
---

## Observation

The City and Sector Financial handler `fn_0044D1BB` loads `DATA/PX08/PX05008`
through resource number `0x1390` (5008) and opens it in the alternate
320-pixel form. Its drawing uses buffer x from 344, which the closing copy puts
at screen x = 128 to 448, at screen y = 124. On the final screen:

- the player portrait is drawn at `(154,141)-(218,205)` (buffer
  `(370,161)-(434,225)`);
- the value column, at buffer x = 610, has its left edge at screen x = 394,
  with a field of four glyph cells on each of the rows y = 151, 160, 178, 196,
  214, 223, 241 and 268, each drawn by the helper `fn_00414187` with width 4;
- the number of contracts is drawn in one bright cell at x = 316 when it is
  below ten and in two cells otherwise, followed by a closing parenthesis drawn
  at x = 322 or x = 328; the opening parenthesis is part of the template.

The handler translates pointer positions from the same left edge of 128 and
tests its close control as the screen rectangle `(161,293)-(210,315)`.

## Interpretation

The Financial panels do not use the 344-by-209 shared panel position: they
show the 320-by-209 source area at `(128,124)`. The choice between City and
Sector is made before the panel opens, and the panel closes only through its
own control. Its values are fields with a fixed left edge at x = 394; drawing
them right-aligned to x = 394 would put every number 24 pixels too far left
and leave the template's zeros showing.

## Alternatives

- The y position of the contract count and which rows hold which amounts are
  not recorded.
- How the handler selects `PX05019` for the Sector variant is not recorded.

## How to reproduce

Start at `fn_0044D1BB`, one of the six callers that open a panel in the
alternate form (FND-OPTIONS-001). It passes `0x1390` to the image loader, calls
`fn_00414187` with a width of 4 at x = 610 in buffer coordinates, and compares
the translated pointer with the close rectangle.
