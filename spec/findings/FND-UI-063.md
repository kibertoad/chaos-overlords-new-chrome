---
id: FND-UI-063
title: Only the About screen, the main console and the detailed sector screen take the right button, and the held-button helper acts at once on a right press
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00464D53
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004718EE..0x00471F05
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00470E24..0x004716EA
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00419022..0x0041953D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00418821..0x00418CCB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00465B64
tool: Ghidra 12.1.3
environment: null
---

## Observation

The right button reaches a screen as event types 17 (press), 18 (release) and
19 (double-click) (FMT-STATE-009, FND-UI-020). Every instruction of the game
code from `0x00401000` to `0x00478000` that compares a value with 17, 18 or 19,
and every `case` with those values in the decompilation of each of its
functions, was listed. Outside the event code they compare other things: the
computer players' selector switch of `fn_00402D70`, packet types in the network
functions `fn_0041D19E` and `fn_0041D33C`, the step codes of the Detailed
Combat timeline in `fn_00430C23`, a menu command in `fn_00414D8C`, a window
message in `fn_004653DE`, and record kinds in the save functions `fn_0046981D`
and `fn_0046A115`. Three functions test the event type against them:

- The About screen `fn_00464D53` (FND-UI-007, FND-UI-055) ends its wait on
  event types 2, 3, 4, 17 and 18: a key, a press or a release of either button.
- The main-console dispatcher `fn_004718EE` (FND-UI-032), which both planning
  handlers call first, takes types 3, 5, 17 and 19 for its eight tiles. For a
  tile hit, it passes the case and whether the type was 17 or 19 to the
  pressed-control helper `fn_00419022(case, right)`.
- The sector-view handler `fn_00470E24` handles types 3 and 17 in one branch
  (FND-UI-015): the back control through `fn_00418821(3, ...)`, the cards
  through `fn_00414D8C(card, point, 1)`, the group order strip through
  `fn_0041462F`, the Overlord bar portraits through `fn_00410770(sector, p)`
  and the Hire dock through `fn_00416C75(point, 1)`. Type 19 reaches nothing
  there, and type 5 has a branch of its own.

No other function tests these types. The 40 functions that call the event step
`fn_00462579`, which include every panel, picker, setup, hand-off, endgame,
combat and report loop, test none of the three, and neither does the city
handler `fn_00470A34`, whose pointer cases are 3 and 5.

The pointer record that `fn_00465B64` returns ends with the dword at
`0x004985A4`, whose third byte `0x004985A6` is set while the left button is
held and fourth byte `0x004985A7` while the right is held (FND-UI-020).

- `fn_00419022(case, right)` draws the tile's pressed art, plays slot 2, and
  loops while the right-button byte is set when `right` is nonzero and while the
  left-button byte is set otherwise, following the pointer in and out of the
  tile. It succeeds when that button comes up inside.
- `fn_00418821` draws its pressed face and loops only while the left-button
  byte is set. Reached by a right press, the loop does not run: it draws the
  released face again and returns success at once.
- The Hire handler's wait for a press on an offer (FND-HIRE-008) and the card
  handler's wait for a press on a portrait (FND-UI-044) both end when the
  left-button byte is clear, so after a right press they end on their first
  pass with no drag and no change. The Hire handler's reject gate goes through
  `fn_00418821(2, ...)`.

## Interpretation

Every screen ignores the right button except three. Any key or button closes
the About credits. On the city and detailed sector screens a right press or
right double-click on a console tile presses it as the left button does, held
until the right button comes up. On the detailed sector screen a right press
does what a left press does, with three differences that come from the helpers
following the left button: the back control returns to the city at the press
instead of on a release inside, a press on a Hire offer's reject cross toggles
it at the press, and a press on an offer's portrait or a card's portrait starts
no drag. The right button's double-click does nothing on the sector screen
outside the console.

## Alternatives

None known.

## How to reproduce

In `0x00464D53`, find the switch on the event type whose cases 2, 3, 4, 17 and
18 set the flag that ends the wait. In `0x004718EE`, find the switch whose
cases are 3, 5, 17 and 19, and for each tile the test of the type against 17
and 19 before the call to `0x00419022`. In `0x00419022`, find the loop
condition that reads the fourth byte of the pointer record when the second
argument is nonzero and the third byte otherwise. In `0x00418821`, find the
loop condition on the third byte only. In `0x00470E24`, find the switch case
that takes both 3 and 17. Listing the compares of an event type with 17 to 19
across the game code gives the functions above and no others.
