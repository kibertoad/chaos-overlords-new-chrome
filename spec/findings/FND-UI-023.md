---
id: FND-UI-023
title: Timer slots are flags set by timeSetEvent callbacks, the pointer is set on every call, panels slide by revealing their left columns, and numbers are drawn left to right
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004327C0..0x004327DB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004327DC..0x00432846
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004328BE..0x004328F7
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004328F8..0x00432925
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00464B43
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00494800..0x00494813
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00465BC8..0x00465CEB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487B20..0x00487B23
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045CA1B..0x0045CA1F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041953E..0x004196F4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004196F5..0x00419AA7
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004981F8..0x004981FB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00461313
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00414187..0x004142E6
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004142E7..0x0041454F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00427864..0x00427A08
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00449B78..0x00449BD1
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00462EE2..0x00462F4C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487804..0x0048780B
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function extents are those of FND-EXE-004.

- Timer slots. `fn_004327DC(slot, rate)` does nothing for a slot outside 0 to 3.
  Otherwise it takes the period as `1000 / rate` for a rate of 0 or more (a
  signed divide) or `-rate` for a negative rate, and calls
  `timeSetEvent(period, 20, fn_004327C0, slot, 1)` (`TIME_PERIODIC`), storing
  the timer ID at `0x00494800 + 4 * slot`. The callback `fn_004327C0` writes 1
  to the byte `0x00494810 + slot` and does nothing else. `fn_004328BE(slot)`
  returns that byte and `fn_004328F8(slot)` writes 0 to it; each has 20 callers.
- The title initialization starts slot 0 with rate 6 at `0x0046118F` (a period
  of 166 ms) and slot 1 with rate 10 at `0x0046119B` (100 ms). `fn_00464B43`,
  which starts slot 3 with rate 46, has no callers. No code starts slot 2.
- Slot 0 is read in the event pump `fn_00462579`, in the combat animation
  `fn_00430C23`, in the idle-gang warning `fn_00448718`, in the Item
  Information panel `fn_0044B699` for its rotation, in the Comlink Send panel
  `fn_0045EAB1`, and in the Last Turn Events, Sell, Give and Research panels.
  The pump clears it at `0x00463787` only when its second argument is nonzero.
  Slot 1 is read by the intro (FND-VIDEO-002).
- Pump block. At `0x00462EE2`, while the byte at `0x00487830` is set and slot 0
  is set, the pump runs the planning bar countdown (FND-TIMER-003), then tests
  bit 0 of the dword at `0x00487804`, which it steps and wraps at 8, for the
  Comlink button's blink, and repeats the Comlink alert while the 16-bit value at
  `0x00487808` is below 3, setting it back when it reaches 3. The music poll of
  FND-AUDIO-007 comes from the same pump call.
- Pointer. `fn_00465BC8(shape, force)` returns without a change only when
  `shape` equals the dword at `0x00487B20`, `force` is 0 and the byte at
  `0x00498354` is nonzero. Otherwise it loads and sets the stock cursor for
  shape 0 to 4 (`IDC_ARROW`, `IDC_IBEAM`, `IDC_CROSS`, `IDC_NO`,
  `IDC_WAIT`) and keeps the handle at `0x00498864`. The dword at `0x00487B20` is
  99 in the image and no instruction writes it. Of the 35 calls, 34 pass
  `force` 1. The window procedure's `WM_SETCURSOR` case at `0x0045CA1B` calls
  it with shape 0 and `force` 0.
- Slide in. `fn_0041953E(alternate)` sets the travel to 320 with source x 344
  when `alternate` is nonzero, else 344 with source x 0. The divisor is the
  dword at `0x004981F8` divided by 4 with a signed divide, raised to 1 when
  below 1. The step is the travel divided by the divisor, raised to 16 when 16
  or less. With Slide Panels on (`0x00487840`) it plays slot 0 through
  `fn_00464290` and, for `c = step, 2*step, ...` while `c` is below the travel,
  copies the panel's left `c` columns, rows 144 to 353 of buffer 7 from the
  source x, to the screen rectangle from x `448 - c` to 448 and y 124 to 333.
  With the option on or off it then copies the whole panel to x `448 - travel`
  and sets the byte at `0x004854C8` to 1.
- Slide out. `fn_004196F5(alternate, restore)` uses the same travel and step.
  With Slide Panels on it plays slot 1 and, for `c` from `travel - step` down
  while `c` is above 0, copies the panel's left `c` columns to end at x 448 and
  refills the uncovered strip to their left: from buffer 1 when `restore` is 0,
  and from the primary panel held in buffer 7 when it is nonzero. Last it
  copies the whole area from buffer 1 and clears `0x004854C8`, or, with
  `restore` nonzero, redraws the primary panel.
- The dword at `0x004981F8` is written once, at `0x00461313` in the title
  initialization, with the result of `fn_00432954` (FND-UI-011).
- Numbers. `fn_00414187(buffer, point, value, width, flag)` draws `width` cells
  of 6 by 7 pixels from left to right. It starts with the divisor
  `10^(width-1)` and, for each cell, divides the remaining magnitude by it. The
  first nonzero quotient and every cell after it draw the glyph `quotient + 16`
  of the font strip (`x = 6 * glyph`), from row 0 for a value of 0 or more and
  from row 8 for a negative value, which is drawn by its magnitude. Cells before
  it copy the space glyph at x 0. The last cell is always drawn. The first cell
  takes the whole quotient, so a value with more digits than cells draws glyph
  `16 + quotient` there, a character after `9` in the strip.
- `fn_004142E7(buffer, point, value, width)` draws a value of 0 as `width - 1`
  blank cells from `(0,8)` and the dim 0 at `(354,8)`; any other value as
  `fn_00414187` does.
- Scaled copy. `fn_00427864(src, dst, src_rect, dst_rect, mode)` copies with
  `BitBlt` or the keyed or patterned path chosen by `mode` when the two
  rectangles have the same size. When they differ it calls
  `SetStretchBltMode(COLORONCOLOR)` and `StretchBlt` with `SRCCOPY`, whatever
  the mode.
- `fn_00449B78(point, rect)` returns 1 when the point is inside with the left
  and top edges included and the right and bottom edges excluded.
- Event types. The window procedure maps a key to event 2, `WM_LBUTTONDOWN` to
  3, `WM_LBUTTONDBLCLK` (`0x203`) to 5, painting to 7, and deactivation and
  activation to 8 and 9. `WM_LBUTTONDOWN` sets the byte at `0x004985A4` and
  `WM_LBUTTONUP` clears it. A double click also toggles the byte at `0x00498100`.

## Interpretation

Each timer slot is a flag that the timer thread raises and a loop lowers. Ticks
that come while a loop is busy are lost: a loop sees at most one per pass. The
presentation clock runs at 6 Hz on slot 0 and the input clock of the intro at
10 Hz on slot 1.

The pointer function sets the cursor on every call, since no shape equals 99.
`force` never matters. While the window procedure runs, every `WM_SETCURSOR`
sets the arrow, so the hourglass shows only while the game does work without
handling messages.

A panel slides in by showing more and more of its left edge against the line
x = 448, the right edge of the panel area, so its left edge moves from 448 to
its final place. The last step is whatever is left after the last whole step,
drawn by the final full copy. With a benchmark count below 4 the divisor is 1
and the step is the whole travel, so the slide is a single copy. The slide-out
redraws what was underneath as it goes: the screen's backing buffer, or the
panel it was opened over.

The number helpers show a too-wide value with a punctuation glyph in the first
cell and the remaining digits after it. The red digits of a negative value use
the same x as the green ones.

A scaled copy is always opaque, so a keyed or patterned copy loses its key when
the sizes differ.

## Alternatives

- Whether any scaled caller relies on the key, so that the opaque copy shows,
  has not been checked by a capture.

## How to reproduce

In `0x004327DC`, find the compare with 3, the constant `0x3E8`, the push of
`0x4327C0` and the call to `timeSetEvent`. List the callers of `0x004327DC`,
`0x004328BE` and `0x00465BC8`, the references to `0x00487B20` and the bytes at
`0x00487B20`. In `0x0041953E`, find the read of `0x004981F8`, the shift by 2,
the constants `0x140`, `0x158`, `0x1C0` and `0x14D`. In `0x00427864`, find the
calls to `SetStretchBltMode` and `StretchBlt`.
