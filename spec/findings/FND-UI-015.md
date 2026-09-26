---
id: FND-UI-015
title: The planning loop hands every event to a city handler or a sector-view handler, chosen by one view flag
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046FD80..0x00470A33
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00470A34..0x00470E23
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00470E24..0x004716EA
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004169B3..0x00416C74
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041462F..0x00414D8B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045C33B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487B88..0x00487B8F
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges are those of FND-EXE-004. Rectangles are written as half-open corners
`(left,top)-(right,bottom)`: the rectangle helper `fn_00425EDF` packs
`(top, left, bottom, right)`, the point helper `fn_00425F8C` packs `(x, y)`,
and the test `fn_00449B78` passes them to `PtInRect`, which leaves out the
right column and the bottom row.

Event numbers. The window procedure `fn_0045C33B` stores each message as a
four-field event: type, window, and two values. For a key press (type 2) the
first value is the character and the second the virtual key; for the mouse
the two values are x and y. Types used below: 2 key down, 3 left button down,
4 left button up, 5 left double-click, 0x11 right button down.

The planning loop. The per-player planning function `fn_0046FD80` sets the
byte `0x00487B88` to 1 and the dword `0x00487B8C` to the active player
(`0x004ABC84`), then takes events from the pump `fn_00462579`. Type 1 (menu
commands) it handles itself. Every other event goes to `fn_00470A34` while
`0x00487B88` is nonzero and to `fn_00470E24` while it is 0. The loop that runs
after a player's Done while other players still plan, `fn_00471F06`, makes the
same choice. Both handlers first pass the event to the console dispatcher
`fn_004718EE` (FND-UI-032) and then test their own areas.

`0x00487B88` is written only by `fn_0046FD80` (1 on entry), `fn_00470A34`
(0 when a sector view opens) and `fn_00470E24` (1 when it closes). It is read
by the pump, the Hire dock renderer `fn_00417CBA`, the Hire handler
`fn_00416C75`, `fn_00411D9B` and `fn_00471F06`. `0x00487B8C` is written by
`fn_0046FD80`, by the sector compositor `fn_00410770` (its second argument)
and by the Overlord bar renderer `fn_00413858` (FND-UI-017).

City handler `fn_00470A34`:

- Key down, virtual keys `0x0D` and `0x2B`: clears `0x00487B88` and draws the
  sector view of the selected sector (`0x004ABC80`) for the active player
  with `fn_00410770`.
- Key down, arrows (`0x25` to `0x28`): moves the selection one column or row,
  staying at the same place at the edge of the 8-by-8 grid, and redraws the
  selection with `fn_00411DF5`.
- Left button down in `(2,42)-(434,458)`: takes the sector
  `(x - 2) / 54 + ((y - 42) / 52) * 8` and selects it with `fn_00411DF5`
  unless its sector record's owner byte is -2.
- Left double-click in the same rectangle: the same sector, if its owner byte
  is not -2, becomes the selected sector, `0x00487B88` is cleared and
  `fn_00410770(sector, active player)` draws the sector view.
- Left button down and double-click in `(440,373)-(636,450)` go to the Hire
  handler `fn_00416C75` with mode 1 and 2.

The divisors 54 and 52 differ from the 53-by-51 stride at which the cells are
drawn (FND-UI-033, FND-UI-017), so a click near the right or bottom of the map
selects by a grid that drifts up to four pixels from the drawn one. No
instruction in game code stores -2 in a sector's owner byte; the test can
succeed only for a value that arrives by a block read.

Sector-view handler `fn_00470E24`:

- Key down, `0x0D` and `0x2B`: shows the pressed art of the back control at
  `(4,394)-(36,457)` with `fn_00418CCC(3, ...)`, sets `0x00487B88` to 1,
  redraws the city with `fn_0041066F` and reselects the sector with
  `fn_00411DF5`.
- Key down, arrows: moves the selected sector by -1, -8, +1 or +8 unless it is
  at that edge of the grid, and redraws with `fn_00410770(sector, active
  player)`.
- Left or right button down (types 3 and 0x11):
  - in `(4,394)-(36,457)`: the held-button helper `fn_00418821(3, ...)`; on a
    release inside, the same return to the city as the Enter key;
  - only while `0x00487B8C` equals the active player, in the card area
    `(254,80)-(404,416)`: card `(x > 329) + 2*(y > 192) + 2*(y > 304)`, if the
    card slot table at `0x004ABC68` holds a gang for it, goes to the
    individual command handler `fn_00414D8C(card, point, 1)`;
  - in the same case, in `(253,61)-(405,77)` and only while the flag
    `0x004ABC4C` is set: the sector-wide command handler `fn_0041462F` with 1
    when `x > 367` and 0 otherwise;
  - for each player `p`, in `(12 + 70p, 5)-(74 + 70p, 37)` and only when the
    sector record's byte `0x10 + p` is nonzero: `fn_00410770(sector, p)`;
  - in `(440,373)-(636,450)`: `fn_00416C75(point, 1)`.
- Left double-click (type 5):
  - in `(64,60)-(226,216)`: the cell `(x - 64 > 53) + (x - 64 > 107)` plus
    three times `(y - 60 > 51) + (y - 60 > 103)`. The centre cell 4 is
    ignored, and so is a cell whose flag in the nine-byte table `0x004ABC40`
    is 0. Otherwise the selected sector moves by -9, -8, -7, -1, +1, +7, +8
    or +9 and `fn_00410770(sector, active player)` redraws the view;
  - in the card area, with a gang in the card slot: `fn_00414D8C(card, point,
    2)` while `0x00487B8C` is the active player, and `fn_004169B3` for
    another player's card;
  - in `(86,228)-(206,424)`: site `(y > 294) + (y > 360)` of the sector goes
    to the Site Information handler `fn_0044C476` with the site's definition
    number, 0, and whether the sector's owner is the active player;
  - in `(440,373)-(636,450)`: `fn_00416C75(point, 2)`.

`fn_004169B3(player, card, point)` makes the point relative to the card's
corner `(254 + 76*(card % 2), 80 + 112*(card / 2))`. Inside `(5,20)-(69,84)`,
the portrait, it opens the gang panel `fn_00449E80` with a copy of the gang's
32-byte record. Inside `(5,86)-(25,106)`, `(27,86)-(47,106)` and
`(49,86)-(69,106)` it opens the Item Information panel `fn_0044B699` with the
166-byte item definition at `0x004A5F08 + item * 0xA6` for the gang's weapon,
armor or miscellaneous item (record offsets 4, 5 and 6), when that byte is not
-1.

`fn_0041462F(recurring)` opens a pop-up command menu at `(290,65)`: menu 3
when its argument is 0 and menu 5 when it is 1. Before it opens, the menu
helper `fn_0042548A` greys one of two items depending on whether the active
player owns the sector (items 4 or 7 of menu 3, 2 or 5 of menu 5), greys both
while the sector's police byte (offset `0x0F`) is above 0, and in menu 3 greys
item 1 unless another player has a gang the active player can see in the
sector; `fn_0042533F` enables them again afterwards. It gives the chosen order to every one of the
active player's gangs in the selected sector, uses roster slot 80 of the
player as a scratch record for the Attack, Influence and Move pickers, and
with argument 1 also stores the order as the recurring action. It then
redraws the status marker and the sector view.

## Interpretation

The city screen and the detailed sector screen are one planning loop with two
input handlers, and `0x00487B88` says which screen is up. `0x00487B8C` is the
player whose gangs the sector view shows. It starts as the active player, and
the portraits of the Overlord bar become buttons in the sector view: clicking
a lit portrait shows that player's gangs in the sector, as far as the active
player can see them. Orders can be given only while the view shows the active
player's own gangs; another player's cards open only the information panels.

A sector view opens by a double-click on a city cell or by Enter. Moving to a
neighbour in the nine-sector display takes a double-click. The strip above
the cards gives one order to all the player's gangs in the sector: its left
half for this turn, its right half as a recurring order.

## Alternatives

- Why the Enter test also accepts virtual key `0x2B` has not been found; no
  key on a standard keyboard sends it.
- The contents of pop-up menus 3 and 5 are resources this finding does not
  list.

## How to reproduce

In `0x0046FD80`, find the test of `0x00487B88` before the calls to
`0x00470E24` and `0x00470A34`. In `0x00470A34`, find the constants 54, 52, 42
and the compare with -2. In `0x00470E24`, find the rectangles with the
constants above, the compares with 329, 192, 304, 294, 360, 367, 53 and 107,
and the call to `0x0041462F`. In `0x0045C33B`, find the stores of 2, 3, 4, 5
and 0x11 into the event type at `0x00498360`.
