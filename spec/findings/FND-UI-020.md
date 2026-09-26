---
id: FND-UI-020
title: The window procedure turns Windows messages into a four-number input event that two message pumps hand to the screen loops
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045C33B..0x0045CD6F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045C180..0x0045C2CC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045C2CD..0x0045C33A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045CD70..0x0045CDD2
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00462579..0x004637B7
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00449B78..0x00449CDD
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00465B27..0x00465CEB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00498360..0x0049836F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004980A0..0x00498125
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00498570..0x004985D8
tool: Ghidra 12.1.3
environment: null
---

## Observation

The window procedure `fn_0045C33B` is registered by `fn_00465620`
(FND-PLATFORM-009) and has no direct callers. It first looks up which surface
slot owns the window handle (only slot 0 does, FND-GFX-004). It writes an event
record of four 32-bit numbers at `0x00498360`: type, then `a` at `0x00498364`,
`b` at `0x00498368` and `c` at `0x0049836C`. The messages it handles:

| Message | Event type | What it records or does |
|---|---|---|
| `WM_CREATE` (1) | none | Opens the command-line file at `0x004985D8` with `fn_0042B60F(3, ..., 'CSOG')`; when it opens, closes it with `fn_0042AC7A(3)` and loads it with `fn_0046381A`, whose result goes to `loaded_game_kind` |
| `WM_DESTROY` (2) | 16 | Nothing else |
| `WM_PAINT` (15) | 7, `a` = slot | When `0x00498108` is set, also calls `BeginPaint` and `EndPaint` (`fn_0045CD70`, `fn_0045CDA4`) at once, drawing nothing |
| `WM_CLOSE` (16) | 6, `a` = slot | Nothing else |
| `WM_ACTIVATEAPP` (28) | 9 when activated, 8 when not | Keeps the flag at `0x00487894` |
| `WM_SETCURSOR` (32) | none | Calls `fn_00465BC8(0, 0)`, the arrow |
| `WM_KEYDOWN` (256) | 2, `a` = character, `b` = virtual key | See below |
| `WM_CHAR` (258) | none | Stores the character's low 16 bits at `0x004877C4` |
| `WM_COMMAND` (273) | 1, `a` = bits 8 to 15 of the command, `b` = bits 0 to 7 | The high word of `wParam` (1 for an accelerator) is dropped |
| `WM_MOUSEMOVE` (512) | none | Client point from `lParam` to `0x0049859C`; screen point from `GetCursorPos` to `0x004985A0` |
| `WM_LBUTTONDOWN` (513) | 3, `a` = slot, `b` = x, `c` = y | Clears `0x00498100`; sets the left-button bytes `0x004985A4` and `0x004985A6` |
| `WM_LBUTTONUP` (514) | 4, same numbers | Clears `0x004985A4` and `0x004985A6` |
| `WM_LBUTTONDBLCLK` (515) | 5 or 3 | Flips `0x00498100`; type 5 when it is now set, 3 when now clear |
| `WM_RBUTTONDOWN` (516) | 17, same numbers | Clears `0x00498100`; sets `0x004985A5` and `0x004985A7` |
| `WM_RBUTTONUP` (517) | 18 | Clears `0x004985A5` and `0x004985A7` |
| `WM_RBUTTONDBLCLK` (518) | 19 or 17 | Flips `0x00498100` as for the left button |
| `MM_MCINOTIFY` (953) | none | Clears `0x00487790` |
| 1025 | none | Socket notifications of network play: sets flags in one of twelve connection records at `0x004906E8` (stride `0x350`) or the listening record |
| 1026 | none | Sets `0x00498104` |
| 1028 | none | Appends received bytes to the network ring buffer `fn_00423C81(1, ...)` |
| 1029 | none | Stores `wParam` at `0x00490866` (`fn_00424A0D`) |

Every other message goes to `DefWindowProcA`. The coordinates of the button
events are the client coordinates in `lParam`, taken as unsigned 16-bit halves.

Keys: for `WM_KEYDOWN` the procedure keeps the virtual key in `b` and
translates it with `MapVirtualKeyA(key, 2)` into `a`. When
`GetAsyncKeyState(VK_SHIFT)` reports Shift held, it replaces the result for
thirteen keys by the shifted character of a United States keyboard:
`'` to `"`, `,` to `<`, `.` to `>`, `/` to `?`, `;` to `:`, `=` to `+`, and the
digits 0 to 9 to `)!@#$%^&*(`. Letters come out in upper case whether or not
Shift is held, since that translation gives the upper-case letter.

The pumps:

- `fn_0045C180(out, ticks)` clears `0x00498108` and the event record, then
  loops: when `PeekMessageA` sees a message it takes it with `GetMessageA` and
  offers it to `TranslateAcceleratorA` with table 102 before
  `TranslateMessage` and `DispatchMessageA`. Before that it calls
  `IsDialogMessageA` with a null dialog handle, and only when no modeless
  dialog is open (`0x00487B1C` zero), so that call never takes a message. It
  stops when the event
  type is nonzero, when `0x00498104` is set (which it clears), or when
  `timeGetTime` passes the start time plus `ticks * 17` ms. It copies the
  record to `out`. Its four callers are `fn_00462579` (with 1 tick), the
  unreached presenter `fn_004653DE` (FND-UI-009) and two network functions,
  `fn_0042202D` and `fn_004222C6`.
- `fn_0045C2CD()` sets `0x00498108`, takes at most one message with
  `PeekMessageA(..., PM_REMOVE)`, makes the same `IsDialogMessageA` test, and
  translates and dispatches it without the accelerator table. It leaves the
  event record alone and returns nothing. Its 22 callers are the loops that
  animate without waiting for input: the intro, the CD wait `fn_00458D54`, the
  city handlers `fn_00414D8C`, `fn_00416C75` and `fn_00418821`, the setup drag
  `fn_0040F72E`, panel and network loops, and the unreached wipe
  `fn_00464B43` (FND-TIMER-002).

`fn_00462579(out, clear_tick)` is the event step every screen loop calls (40
callers). It calls `fn_0045C180(out, 1)`, so it waits at most 17 ms, and then:

- When the window was deactivated (`0x00487890`) and the flag at `0x00487894`
  shows it active again while the event is not 9, it posts `WM_ACTIVATEAPP`
  with 1 to itself.
- Event 1 with `(0x80, 3)` shows the About screen `fn_00464D53`; `(6, n)` and
  `(7, n)` set `music_level` and `effects_level` to `n - 1` and apply them;
  `(0x84, 1)` Thousands of Colors and `(0x84, 11)` Full Screen show dialog 136
  and flip `pref_thousands_colors` and `pref_full_screen` with their check
  marks; `(0x84, 6..9)` flip Base Statistics, Detailed Combat, Slide Panels
  and Warn if Idle Gangs with their check marks; `(0x85, 1)` Disconnect calls
  `fn_0046D00D`; `(0x85, 3..6)` set `comm_type` to 0 to 3 and redo the Comm
  menu. No branch handles `(0x80, 1)`, Help Topics (FND-HELP-005).
- Events 6 (close) and 16 (destroy) are replaced by event 1 `(0x81, 9)`, Exit.
- Event 8 (deactivated) stops CD music when it plays, minimizes the window
  when `full_screen_active` is set, sets `0x00487890` and invalidates the
  window. Event 9 (activated) restarts music, restores and raises the window,
  clears both flags, applies the palette again, attaches the menu bar, sets
  the arrow, repaints and posts `WM_PAINT`.
- It then runs the timed work of the six- and ten-per-second timers
  (FND-TIMER-002) and copies the possibly rewritten event to `out`.

Helpers:

- `fn_00449B78(point, rect)` returns `PtInRect` for a rectangle stored as four
  16-bit numbers in the order top, left, bottom, right, and a point stored as
  x in the low half and y in the high half. `PtInRect` counts the left and top
  edges as inside and the right and bottom edges as outside.
- `fn_00465B64(out)` returns 16 bytes from `0x00498598`: a zero word, the client
  point, the screen point and the four button bytes, and then copies
  `0x004985A4` and `0x004985A5` into `0x004985A6` and `0x004985A7`.
- `fn_00465B27()` returns whether Escape is held now (`GetKeyState(0x1B)`, high
  bit); `fn_00449CAE` and `fn_00449CC6` call `ShowCursor(0)` and
  `ShowCursor(1)`, each from `WinMain` only.
- `fn_00449BFC(src, dst, n)` copies `n` bytes forward, one at a time.
- `fn_00449C41(s)` scans a string from its second byte to the first zero and
  stores the count in the first byte. The path constants that pass through it,
  such as ` data\PX08\px00128`, begin with a space that this byte replaces.
- `fn_00449BD2(x)` returns `x` minus the runtime `fmod(x, 1.0)`
  (`fn_00478BD0`), the whole part of `x` rounded toward zero.
- `fn_00465BC8(shape, force)` compares `shape` with the value at `0x00487B20`,
  which is 99 in the file and has no writer, so for shapes 0 to 4 it always
  loads and sets the stock cursor (FND-UI-034).

The block `0x004980A0..0x00498125` holds several unrelated globals:
`0x004980A0..0x004980BF` are MCI parameter blocks of the CD code
(`fn_00458B43` and `fn_00458EA6`); `0x004980C0` is the
`PAINTSTRUCT` that `fn_0045CD70` and `fn_0045CDA4` pass to `BeginPaint` and
`EndPaint`, and `0x0049810C` the device context `BeginPaint` returns;
`0x00498100` is the double-click phase above; `0x00498104` the network wake
flag; `0x00498108` the flag that makes the procedure validate a repaint; and
`0x00498110..0x00498125` belong to the Comlink Send panel `fn_0045EAB1`. The 25
panel functions that write the block write only `0x00498100`, setting it to 1
just after their slide-out call.

The block `0x00498570..0x004985DA` holds the window handle at `0x00498570`,
the `MSG` the pumps fill at `0x00498578` (28 bytes), the pointer snapshot of
`fn_00465B64` from `0x00498598` (a zero word, the client point at `0x0049859C`,
the screen point at `0x004985A0`, and the left and right button bytes at
`0x004985A4` to `0x004985A7`), the window class at `0x004985A8` (48 bytes) and
the command-line file name from `0x004985D8` (FND-PLATFORM-009).

## Interpretation

Input reaches the game as polled events. A screen loop calls `fn_00462579`,
gets back at most one event of the types above (or type 0 after 17 ms), and
acts on it; the menu commands that change options are handled for every loop in
one place. The accelerators of table 102 produce the same events as the menu
items, so a loop cannot tell them apart. Loops that animate call
`fn_0045C2CD` instead and see no input events at all, and a repaint arriving
during them only validates the window. The pointer position is read from the
button event, never polled. The keyboard map is fixed to the United States
layout. The rectangles every screen tests with `fn_00449B78` are stored top,
left, bottom, right, and a press on the right or bottom edge is outside.
Because the remembered pointer shape is never written, every `WM_SETCURSOR`,
which Windows sends on each pointer move, sets the arrow, even while code
elsewhere has asked for the hourglass.

## Alternatives

What a panel does when it sets `0x00498100` on closing (FND-UI-021) is read
from the flag's use in the window procedure: the next double click then counts
as a plain press. It has not been observed.

## How to reproduce

The class registration at `0x00465939` stores `0x0045C33B`. In
`0x0045C33B`, the message tests are at `0x0045CC5E` and after; the shifted-key
table is the switch at `0x0045CBB4`. The pumps' `PeekMessageA` calls are at
`0x0045C1DA` and `0x0045C2E7`. The event step's Help test is absent between
`0x00462620` and `0x004628B9`. `PtInRect` is called at `0x00449BC2`.
