---
id: FND-COMLINK-007
title: Comlink Send tests player_active and players_human, starts from a blank draft, wraps the cursor between rows, and View draws a 64-by-64 portrait; the positions of both panels' fields
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045EAB1..0x0045FDF0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045FDF1..0x004600D1
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004600D2..0x0046023B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046023C..0x00460390
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045E04D..0x0045E7CD
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045E7CE..0x0045EAB0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00498110..0x00498119
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004ABC58..0x004ABC5D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048735C
tool: Ghidra 12.1.3
environment: null
---

## Observation

Coordinates are panel-local, with the panel at (104, 124) on the screen, unless
marked as screen coordinates. `PX00129` is the sheet held in surface 6.

Send, `fn_0045EAB1`, when it opens:

- It loads `PX05018`, or resource 5023 when the byte `g_0048735C` is nonzero.
  That byte is 0 in the executable's data and no instruction writes it; its
  four references are reads. The same byte enables an extra control drawn at
  screen (137, 140) by `fn_004604A7`.
- It stores 1 in the caret phase byte `0x00498110` and 0 in the six selection
  bytes at `0x00498114`, one per player slot.
- It builds six eligibility bytes in a stack local. Slot `p` is eligible when
  the byte at `0x004ABBE0 + p` (`player_active`) and the byte at
  `0x004ABC58 + p` are both nonzero; then the byte of `active_player` is
  cleared. The setup functions `fn_00456F80` (`0x00457A3C..0x00457A6D`) and
  `fn_004677F0` (`0x00468821..0x00468852`) set `0x004ABC58 + p` to 1 when
  `controller[p]` is 0 or 3 and to 0 otherwise. The event pump stores 0 there
  at `0x00462B8C`, in the branch that also sets `controller[p]` to 1.
- The text cursor's column and row are stack locals that start at 0, and the
  "some recipient selected" flag is a local byte that starts at 0.
- For each slot `p` it draws a card at the point (98, 20), (98, 54), (98, 88)
  for slots 0 to 2 and (219, 20), (219, 54), (219, 88) for slots 3 to 5: a
  7-by-30 strip at (+1, +1) filled with the player's colour (three 16-bit
  components at `0x004ABC18 + p * 6`), the 32-by-32 portrait from `PX00129` at
  (`portrait * 32`, 480) at (+8, 0), and the name from the second byte of the
  player-name record at (+42, +2). For a slot that is not eligible each colour
  component is divided by 4, the portrait comes from y 594 and the name is
  drawn by `fn_0041B7D6`, which takes its glyphs from the `PX00129` row at
  y 274, where `fn_00413FD5` takes them from y 0.
- The renderer `fn_0045FDF1` draws a one-pixel frame, with GDI `Rectangle` and
  the null brush, of 105 by 34 at (97, 19) plus the same column and row
  offsets: black while the slot's selection byte is 0, green (0, 0xFFFF, 0)
  while it is set. With its argument 1 it also draws the four text rows at
  (95, 132 + 8 * row).

Send, input:

- A pointer press or double-click outside the panel plays slot 4
  (`0x0045FAFA`). Inside, Cancel goes through `fn_00418821(1)` and closes the
  panel on a release inside. Send with the selected flag clear plays slot 4;
  otherwise it goes through `fn_00418821(0)` and, on a release inside, closes
  the panel and sends (FND-COMLINK-006). Execute (`0x2B`) does the same with
  `fn_00418CCC(0)` for the pressed face.
- A card press on a slot that is not eligible plays slot 4. On an eligible slot
  it flips the selection byte between 0 and 1, redraws the cards, copies them
  to the screen, sets the selected flag to whether any selection byte is set,
  and draws the Send face through `fn_00418E66` with that flag.
- Any other key event first draws the character under the cursor again with
  its plain glyph. Then Backspace (`0x08`) sets an erase flag and subtracts 1
  from the column; Enter sets the column to 0 and adds 1 to the row; Left,
  Up, Right and Down subtract 1 from the column, subtract 1 from the row, add
  1 to the column and add 1 to the row.
- In the same event, a character from `a` to `z` has 0x20 subtracted. A
  character from 0x20 to 0x5A is written at the cursor through `fn_004600D2`,
  and 1 is added to the column.
- Then: a column below 0 becomes 39 with 1 subtracted from the row; a column
  above 39 becomes 0 with 1 added to the row; the row is held between 0 and 3.
  When the erase flag is set, a space is written at the resulting cursor. The
  caret is drawn last.
- `fn_004600D2` stores the character at `0x00498125 + row * 40 + column` and
  draws the glyph at (`(c - 0x20) * 6`, 0) of `PX00129`, 6 by 7, to the screen
  at (199 + 6 * column, 256 + 8 * row) and to the panel copy at
  (95 + 6 * column, 132 + 8 * row). The caret helper `fn_0046023C` draws the
  same cell from y 441 while `0x00498110` is 0 and from y 0 while it is set.

View, `fn_0045E04D`, after the read and pending updates of FND-COMLINK-004:

- The message number (cursor + 1) and the count, each as two digits with a
  leading zero, at (34, 13) and (70, 13). The arrow faces at (31, 33) and
  (59, 33) come from the same `PX00129` rectangles as in the Last Turn Events
  panel (FND-EVENT-005), and the step helper `fn_0045E7CE` draws the same
  pressed faces at screen (135, 157) and (163, 157).
- The year `turn / 52 + 2050` as four digits at (95, 20) and the week
  `turn % 52 + 1` as two digits with a leading zero at (125, 20), with signed
  division of the 16-bit `turn`.
- A black fill of (95, 38)-(155, 45), then the first 10 characters of the
  sender's name at (95, 38).
- An 8-by-64 strip at (95, 46) filled with the sender's colour.
- The sender's 32-by-32 portrait from `PX00129` at (`portrait * 32`, 480),
  copied into the 64-by-64 rectangle at (103, 46); the copy routine
  `fn_0042773E` uses `StretchBlt` in `COLORONCOLOR` mode when the rectangles
  differ in size.
- The four 40-character rows at (95, 121 + 8 * row), drawn by `fn_00413FD5`.
- The View handler `fn_0045D61A`, like Send, plays slot 4 for a pointer press
  or double-click outside the panel rectangle (`0x0045DD3F`) and tests its
  controls only for one inside it.

## Interpretation

Only human players still in the match can receive messages: a network player
who drops loses the human flag with the controller change. Each opening of Send
starts a fresh message with no recipient selected and the cursor at the top
left; nothing typed in an earlier opening survives. The recipient cells follow
player slots: slots 0 to 2 down the left column, 3 to 5 down the right.

The cursor moves through the grid as through one line of 160 cells, except
that the ends do not wrap: Right or a typed character at the end of row 3
returns the cursor to the start of row 3, and Left or Backspace at the start
of row 0 goes to the end of row 0. Backspace moves back one cell and blanks the
cell it arrives at.

Resource 5023 is never loaded in this build.

## Alternatives

- The Send face drawn by `fn_00418E66` presumably shows Send dimmed while no
  recipient is selected; the faces it copies were not read.
- Whether a key event can carry both a virtual key from the switch and a
  printable character, which would move the cursor twice, depends on the event
  pump and was not traced.

## How to reproduce

In `fn_0045EAB1`, read the test of `0x0048735C` before the loads of 5018 and
5023, the stores into `0x00498110` and `0x00498114`, the eligibility loop over
`0x004ABBE0` and `0x004ABC58`, the six card points, the virtual-key switch
with the erase flag, the range test on the character and the wrap block. Open
`fn_0045FDF1` for the frame, `fn_004600D2` and `fn_0046023C` for the cells,
and `fn_0045E04D` for the View fields.
