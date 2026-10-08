---
id: FND-COMLINK-010
title: Comlink Send edits a fixed grid of four rows of 40 characters from space to Z, wraps the cursor between rows, and switches the caret every third tick of a 6 Hz timer
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045EAB1
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045F2E4..0x0045F491
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045FD4B..0x0045FDB3
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004600D2
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046023C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004327C0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00432926
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00494810..0x00494814
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004328BE
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004328F8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00460CCF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004327DC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00462579
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046A80A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00498110
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00498125..0x004981C5
tool: Ghidra 12.1.3
environment: null
---

## Observation

Key events in the Send handler `fn_0045EAB1`, whose text cursor is a column
and a row kept as 16-bit stack words that start at 0:

- Each key event other than Execute first draws the cell under the cursor
  again through `fn_004600D2` (`0x0045F2E4`) and clears an erase flag. Then
  it switches on the virtual key: Backspace (`0x08`) sets the erase flag and
  subtracts 1 from the column, Enter (`0x0D`) sets the column to 0 and adds 1
  to the row, and Left, Up, Right and Down (`0x25` to `0x28`) subtract 1 from
  the column, subtract 1 from the row, add 1 to the column and add 1 to the
  row.
- The character the event carries is then lowered by `0x20` when it lies from
  `0x61` to `0x7A`. It is written at the cursor through `fn_004600D2`, and 1 is
  added to the column, only when it lies from `0x20` to `0x5A`
  (`0x0045F3D6..0x0045F40C`: `CMP EAX,0x20`, `JL`, `CMP EAX,0x5A`, `JG`).
- Then, in this order: a column below 0 becomes 39 and 1 is subtracted from
  the row; a column above 39 becomes 0 and 1 is added to the row; a row below
  0 becomes 0 and a row above 3 becomes 3 (`0x0045F40C..0x0045F45E`). When the
  erase flag is set a space is written at the resulting cursor (`0x0045F477`),
  and last the caret is drawn there through `fn_0046023C` (`0x0045F489`).

Cells and caret:

- `fn_004600D2` stores the character at `0x00498125 + row * 40 + column` and
  draws the 6-by-7 glyph from x `(c - 0x20) * 6`, y 0 of `PX00129` (surface
  6) to the screen at `(199 + 6 * column, 256 + 8 * row)`, and again into
  surface 7 at `(95 + 6 * column, 132 + 8 * row)`.
- `fn_0046023C` draws the cell under the cursor at the same screen point from
  y 441 while the byte `0x00498110` is 0 and from y 0 while it is set. The
  Send handler stores 1 there when the panel opens.

Timer:

- The timer callback `fn_004327C0` sets byte `0x00494810 + n` to 1 for the
  timer number it is given. `fn_00432926(n)` sets the same byte for an `n` from
  0 to 3, `fn_004328BE(n)` returns it and `fn_004328F8(n)` clears it.
- `fn_004327DC(n, frequency)` calls `timeSetEvent(1000 / frequency, 20,
  fn_004327C0, n, 1)` for a positive frequency. `fn_00460CCF` calls it with
  timer 0 and frequency 6 (`0x0046118F`), so timer 0 fires every `1000 / 6` =
  166 milliseconds in integer division. No other call starts timer 0
  (FND-UI-023), so the Send handler does not restart it.
- At the end of each pass of its event loop (`0x0045FD4B..0x0045FDB3`) the
  Send handler tests flag 0. When it is raised, it clears it, adds 1 to a
  counter that starts at 0 when the panel opens, and on the count of 3 sets the
  counter to 0 and flips `0x00498110` between 0 and 1. Every consumed event,
  whether or not it flips the phase, draws the caret again.
- The Send handler neither calls `fn_00432926` nor writes `0x00494810`. The
  only caller of `fn_00432926` is `fn_0046A7CB` (`0x0046A80A`), which raises
  flag 0 itself before its own loop.
- The event pump `fn_00462579`, which the Send loop calls with a second
  argument of 0, tests flag 0 and clears it only when that argument is nonzero
  (FND-EXE-004 gives its range).

EXP-COMLINK-001 typed keys into this grid. `[`, `` ` ``, `\` and `]` were
dropped. Backspace at column 0 of row 0 went to column 39 of row 0 and blanked
it, the next character typed there moved the cursor to column 0 of row 1, Down
and Up stopped at rows 3 and 0, Enter on row 3 went to column 0 of row 3, and
the 41st character typed into row 3 landed at column 0 of row 3. Enter never
sent the message.

## Interpretation

The message is a fixed four-by-40 grid that the player types over. Characters
from space to `Z` are kept; `[` (`0x5B`) and everything above it are dropped.

The cursor moves through the grid as through one line of 160 cells: Left,
Right, Backspace and typing cross between rows, and Up and Down stop at the
first and last rows. The two ends of that line stay in their own rows: Left or
Backspace at the start of row 0 goes to the end of row 0, and typing or Right
at the end of row 3 goes to the start of row 3. Backspace moves back one cell
and then blanks the cell it arrives at.

The caret starts plain and switches between the plain and the inverse cell on
every third timer-0 event the Send loop consumes, so each phase after the
first lasts three periods, 498 milliseconds. Timer 0 runs from start-up and is
not restarted when the panel opens, so the first phase ends on the third tick
after the opening: more than two periods and at most three after it. When flag
0 is already raised as the panel opens, which depends on when another loop
last cleared it, the first pass counts that tick and the first phase lasts
more than one period and at most two.

## Alternatives

- How often flag 0 is already raised as Send opens was not measured.

## How to reproduce

In `fn_0045EAB1`, read the jump table at `0x0045F349`, the character tests
from `0x0045F3B7` to `0x0045F408`, the wrap block to `0x0045F45E` and the
calls at `0x0045F477` and `0x0045F489`; then the timer block from `0x0045FD4B`.
Open `fn_004600D2` and `fn_0046023C` for the cell arithmetic and the source
rows. List the references to `0x00494810` and the callers of `fn_00432926`.
Open `fn_00460CCF` at `0x0046118F` and follow the call to `fn_004327DC`, which
imports `timeSetEvent`. In `fn_00462579`, read the tests of flag 0 and the
clear guarded by its second argument.
