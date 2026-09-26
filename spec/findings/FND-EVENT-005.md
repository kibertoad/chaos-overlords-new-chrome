---
id: FND-EVENT-005
title: The Last Turn Events panel refuses an empty table, captions each report from strings 33 to 44 by type and cash-failure argument, and animates the researched item
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044F2FC..0x0044FD6B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044FD6C..0x00451601
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00451602..0x004518D8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004702A5..0x00470356
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00494870..0x0049488F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004948EC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00463267
tool: Ghidra 12.1.3
environment: null
---

## Observation

Coordinates below are panel-local unless marked as screen coordinates. The
handler `fn_0044F2FC` loads `PX05010` into the rectangle x 0..344, y 144..353
of work surface 7 and copies that rectangle to the screen at (104, 124) after
every page change (for example at `0x0044F5B0`), so the panel origin is
(104, 124). Surface 6 holds `PX00129` (FND-UI-031). The page index is the
global `g_004948EC`; the compositor `fn_0044FD6C` reads the record with that
index directly.

Opening:

- The handler counts the occupied bytes of the player's 32 records. With none
  it plays general-effect slot 4 (`0x0044FD4E`) and returns without drawing
  anything.
- The handler never resets the page index. The human planning handler
  `fn_0046FD80` stores 0 in it at `0x0046FE6D`, once per planning visit.
  Opening the panel again from the Events control (`fn_004718EE`, call at
  `0x004719A9`) shows the page shown last.
- `fn_0046FD80` opens the panel by itself only when record 0 of the viewer is
  occupied (`0x004702B4`). Before the call it sets each byte of the 32-byte
  table `g_00494870` to 1 for an unoccupied record and to 0 for an occupied
  one, and sets the flag `g_00487814` to 1.
- The compositor sets `g_00494870[page]` to 1 each time it draws a page
  (`0x0044FD7D`) and then sets `g_00487814` to 1 when any of the 32 bytes is
  still 0, and to 0 otherwise. While `g_00487814` is set, the event pump
  `fn_00462579` copies the 8-by-16 rectangle at (488, 512) of `PX00129` to the
  screen at (540, 126) in the branch it takes when bit 0 of `g_00487804` is
  clear (`0x00463267`).

Drawing a page, in the compositor's order:

- The page number (index + 1) and the count, each as two digits with a leading
  zero, at (34, 13) and (70, 13).
- The Previous face at (31, 33), 26 by 23, from `PX00129` at (170, 363) on the
  first page and (118, 363) otherwise. The Next face at (59, 33) from (196,
  363) on the last page and (144, 363) otherwise.
- The illustration area is x 94..336, y 11..169, 242 by 158. Type 4 draws the
  site picture there (FND-UI-016); `PX06004` is loaded into a spare part of
  surface 7 and copied over the area with the transparent copy mode 1. Type 5
  loads `PX06005` into the area, loads resource `4000 + w` into x 0..720,
  y 0..48 of surface 7, where `w` is the 16-bit word at `0x004A5F26 + item * 0xA6`, `0x1E` bytes
  after the item's name,
  and copies its first 48-by-48 frame to (192, 66). Type 9 loads `PX06009` and
  copies the 32-by-32 portrait at (`p * 32`, 480) of `PX00129`, where `p` is
  the byte at `0x004A5F00 + a1`, stretched into the 48-by-48 rectangle at
  (136, 113). Every other type loads resource `6000 + type` into the area.
- It fills (194, 174)-(332, 181) and (122, 183)-(332, 190) with black.
- When `elapsed_turns` (`0x0049CA68`) is above 0, the year
  `(elapsed_turns - 1) / 52 + 2050` as four digits at (122, 174) and the week
  `(elapsed_turns - 1) % 52 + 1` as two digits with a leading zero at
  (152, 174). At 0 no date is drawn.
- The subject text at (194, 174) and the caption, a string resource loaded by
  `fn_00466673` with the length argument 35, at (122, 183).

| Type | `a1` | Caption | Subject |
|---|---|---|---|
| 0 | any | 33 | None |
| 1 | any | 34 | Sector label of `a1` |
| 2 | any | 35 | Sector label of `a1` |
| 3 | any | 36 | Sector label of `a1` |
| 4 | any | 37 | Sector label of `a1`, the string `:` at (206, 174), then from (212, 174) the name of the site definition held in slot `a2` of sector `a1` |
| 5 | any | 38 | Name of item `a1` |
| 6 | 1 | 39 | Sector label of `a2` |
| 6 | 2 | 40 | Sector label of `a2`, `:` at (206, 174), then from (212, 174) the name of gang definition `a3`, cut to 20 characters by writing 0 at its byte `0x14` and restoring it afterwards (`0x00451169`, `0x004511FC`) |
| 6 | 4 | 41 | Name of gang definition `a2`, not cut |
| 6 | other | None | None |
| 7 | any | 42 | Sector label of `a1` |
| 8 | any | 43 | Name of gang definition `a1` |
| 9 | any | 44 | The name in the 12-byte player-name record of `a1`, from its second byte |

A sector label is two characters: `A` plus `sector % 8`, then `1` plus
`sector / 8`, with C's signed division. Site names come from the site
definition table at `0x004AB668` (stride `0x3E`), item names from
`0x004A5F08` (stride `0xA6`) and gang definition names from `0x004A2800`
(stride `0x9C`).

Input:

- Key and pointer events share one switch. A pointer press or double-click
  (events 3 and 5) outside the panel rectangle plays slot 4 (`0x0044FA43`).
- Previous and Next, by pointer or by the Left and Right keys, go through
  `fn_00451602`. It plays slot 3 first, then draws the pressed face straight to
  the screen at (135, 157) for Previous and (163, 157) for Next, taken from
  `PX00129` at (66, 363) and (92, 363), 26 by 23. For a pointer it follows the
  pointer until the button is released, drawing the pressed face while the
  pointer is inside and the plain face ((118, 363) or (144, 363)) while it is
  outside, and changes the page only when the release is inside. For a key it
  waits through `fn_00464CD9(1)` and changes the page. It draws the plain face
  in both cases before returning.
- The exit control goes through the shared helper `fn_00418821` with button 0,
  whose pressed face is `PX00129` (50, 386), 50 by 23, and whose plain face is
  (0, 386), at screen (137, 293).
- While `g_004948F8` is set (the compositor sets it for type 5 and clears it
  for every other type), each timer-0 event consumed by the handler advances a
  frame counter 0, 1, ..., 14, 0, and copies frame `n`, the rectangle
  (48 * n, 0)-(48 * n + 48, 48) of surface 7, to the screen at (296, 190). The
  counter is set to 0 when the panel opens and after each page change.

## Interpretation

The panel has four text fields: page and count in the header, the date, a
subject (sector, site, item, gang or player name) and a caption chosen by the
type, and for type 6 by the order that failed. A cash failure whose argument is
not 1, 2 or 4 shows only the illustration and the date. The researched item
spins through the 15 frames of its strip at the six-per-second rate of timer 0
(FND-UI-001). The Events light blinks while some report has not yet been shown
on the panel.

## Alternatives

- The flag `g_00487814` also drives the Events control's light in the pump's
  other phase; which of the pump's two branches draws the light and which
  restores the plain control was not traced beyond the copy named above.
- The six-per-second rate assumes the handler consumes every timer-0 event;
  a slow machine may skip frames.

## How to reproduce

Open `fn_0044F2FC` from its load of resource 5010. Read the count of occupied
bytes and the slot-4 branch, the rectangle tests and the calls to
`fn_00451602` and `fn_00418821`, and the timer-0 block with the counter that
wraps after 14. Open `fn_0044FD6C` for the store into `0x00494870`, the digit
draws, the four arrow copies, the branches on types 4, 5 and 9, the black
fills, the date arithmetic with 52 and `0x802`, and the switch on the type with
the string IDs `0x21` to `0x2C`. In `fn_0046FD80`, read the test of record 0 at
`0x004702B4` and the store of 0 into `0x004948EC` at `0x0046FE6D`.
