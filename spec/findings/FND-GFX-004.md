---
id: FND-GFX-004
title: The display layer draws with GDI into twelve surface slots, copies the 640-by-460 backing surface to the window's client origin, and uses DirectDraw only to take the screen in full screen
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00425850..0x00425D96
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00425D97..0x00425E98
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00425E99..0x00426F76
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042773E..0x00427A08
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00427E60..0x004282A9
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00428E6A..0x00428EEC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042B7E3..0x0042B7F2
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00449B20..0x00449B77
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004935F8..0x00493A2B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004874E0..0x004874FF
tool: Ghidra 12.1.3
environment: null
---

## Observation

Value helpers. The layer keeps geometry as 16-bit numbers:

- `fn_00425F8C(a, b)` packs two numbers into a point or size, `a` in the low
  half. Points hold x low and y high; `WinMain` passes the size 640 by 480 as
  `fn_00425F8C(640, 480)`.
- `fn_00425EDF(a, b, c, d)` packs a rectangle of four numbers in the order
  top, left, bottom, right. The screen area is `fn_00425EDF(0, 0, 460, 640)`.
- `fn_00425F4D(point, w, h)` makes the rectangle with corner `point` and size
  `w` by `h`; `fn_00425F14(p1, p2)` makes one from two corners and has no
  callers.
- `fn_00425E99(out, r, g, b)` makes a colour of three 16-bit components from
  bits 8 to 15 of each argument, so 65535 gives 255.

Surfaces. Twelve slots of 44 bytes start at `0x00493658`: `+0` in use, `+4`
kind (1 the window, 2 a memory bitmap), `+8` the device context, `+12` the
window handle, `+16` a flag, `+18` the rectangle, `+28` the depth, `+32` the
bitmap, `+36` the palette flag, `+40` the palette handle.

- `fn_00425FB0(slot, kind, w, h, depth, palette)` works for slots 1 to 11. It
  releases the slot, records the rectangle and depth, and for kind 2 creates a
  device context and a bitmap compatible with the window's, and fills it with
  `PatBlt(PATCOPY)` through the stock white brush. With `palette` 0 or more it
  applies the palette `fn_004282AA(slot, 2)`. Its callers are `fn_004622D4`
  (FND-PLATFORM-009) and the image loaders, which use slot 10 for each file.
- `fn_00426202(slot)` releases slots 1 to 11 only, and only when the slot is not
  among the first twelve entries of the target stack.
- A stack of drawing targets starts at `0x004935F8` with its depth at
  `0x00493868`. Entry 0 is slot 0, the window. `fn_0042639F(slot)` pushes a slot
  that is in use while the depth stays below 23; `fn_00426405` pops. Every shape
  primitive draws into the slot on top.

Shape primitives. Each creates a one-pixel solid pen (`CreatePen(PS_SOLID,
1, ...)`), keeps the old pen and brush at `0x00493870` and `0x00493874`, and
deletes what it created:

- `fn_00426575(rect, colour, fill)` calls `Rectangle` with left, top, right,
  bottom taken from the rectangle, with a solid brush of the same colour when
  `fill` is set and the stock `NULL_BRUSH` otherwise. The colour's three
  components become `RGB(r, g, b)`. `Rectangle` leaves out the right column and
  the bottom row.
- `fn_004266A6(rect, colour, fill, shade)` does the same, except that with
  `fill` set and `shade` from 0 to 2 it paints the rectangle's size into the
  scratch surface 11 and composites it through the pattern compositor
  `fn_00427E60` (FND-UI-031). The pattern comes from `0x00494868`; `shade` only
  decides whether this path is taken.
- `fn_00426427(p1, p2, colour)` draws a line with `MoveToEx` and `LineTo`, which
  leaves out the end point; `fn_004264D4` does the same with a palette index.
  `fn_00426909` is the rectangle and `fn_00426A37`, `fn_00426B7D` the ellipse
  (`Ellipse`) with an RGB colour or a palette index; `fn_00426CAB` and
  `fn_00426E1D` draw the ellipse of a centre and a radius.

Copies:

- `fn_0042773E(src, dst, src_rect, dst_rect)` copies with `BitBlt(SRCCOPY)` when
  the two rectangles have the same size and otherwise sets `COLORONCOLOR` and
  calls `StretchBlt(SRCCOPY)` (FND-PLATFORM-008). `fn_00427864` adds the copy
  mode of FND-PLATFORM-008.
- `fn_00449B20(value)` sets `0x00494868` to 2 below 86, 0 from 86 to 170 and 1
  above; `fn_00427E60` loads bitmap 143 for 0, 146 for 1, 147 for 2 and 143 for
  anything else.

No callers. `fn_00426427`, `fn_004264D4`, `fn_00426909`, `fn_00426A37`,
`fn_00426B7D`, `fn_00426CAB`, `fn_00426E1D`, `fn_00425F14`, `fn_00428E6A`
(returns 1) and `fn_0042B7E3` (returns at once) have no callers and no
references, and their entry addresses occur nowhere in the file as 32-bit
values.

Display setup `fn_00425850(size, depth, show)`:

- It clears the twelve slots and the stack, saves the 24 system colours
  0 to 23 at `0x00493878` and the non-client metrics at `0x004938D8`
  (`SPI_GETNONCLIENTMETRICS`, 340 bytes).
- Windowed (`full_screen_active` clear): it sets a rectangle left 32, top 32,
  right `640 + 3`, bottom `480 + iMenuHeight + 8` (the menu height from the
  saved metrics), adjusts it with `AdjustWindowRectEx` for style `0x00CA0000`
  (`WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX`), no menu and extended style
  `0x00040000` (`WS_EX_APPWINDOW`), and creates the window with style
  `0x10CA0000` (the same plus `WS_VISIBLE`) at the adjusted left and top, passing
  the adjusted right and bottom as width and height. The depth is the display's
  `BITSPIXEL`, raised to 8 or lowered to 16.
- Full screen: it creates a `WS_POPUP | WS_VISIBLE | WS_SYSMENU` window
  (`0x90080000`) at 0, 0 of 640 by 480. It calls `DirectDrawCreate`, sets the
  cooperative level `0x15` (`DDSCL_FULLSCREEN | DDSCL_NOWINDOWCHANGES |
  DDSCL_EXCLUSIVE`) and the display mode 640 by 480 at the depth asked for,
  falling back from 16 to 8. It keeps the object at `0x004874E0` and the GDI
  surface at `0x004874E8`. It copies the metrics to `0x00493E30`, sets
  `iMenuHeight` to 18 and the menu font to `MS Sans Serif` (string at
  `0x004874F0`), height -8, weight 400, quality 1, and applies them with
  `SPI_SETNONCLIENTMETRICS` and `SPIF_SENDCHANGE`. When `DirectDrawCreate` fails
  the depth is 0.
- It calls `SetSysColors` with the table at `0x00487480` only when the byte at
  `0x004874EC` is set; that byte is zero in the file and nothing writes it.
- It gets the window's device context into slot 0 (kind 1, rectangle top 0,
  left 0, bottom 480, right 640), shows and updates the window and returns the
  depth.

Display undo `fn_00425D97` releases slots 1 to 11, selects slot 0's saved
palette back, calls `ChangeDisplaySettingsA(NULL, 0)`, and in full screen puts
back the saved metrics and calls `RestoreDisplayMode`; the system colours are
put back only under the same unset byte.

The palette of an 8-bit surface is also attached to the DirectDraw GDI surface
with `CreatePalette` and `SetPalette` when the GDI surface exists
(`fn_004282AA`, FND-PLATFORM-007).

The window's image. Code that draws a screen or panel writes into surface 1
(640 by 460) or directly into slot 0, and copies from surface 1 or the sheet
surface 6 to slot 0 with destination top 0 at the top of the client area. The
repaint event 7 is handled by the loop that receives it; `WinMain` and the main
console copy the whole rectangle top 0, left 0, bottom 460, right 640 from
surface 1 to slot 0 between `BeginPaint` and `EndPaint` (`0x00461D49`,
`0x00470623`). The timed steps copy only the cells they change, for example the
Comlink light's rectangles in `fn_00462579`.

## Interpretation

All drawing is GDI: DirectDraw only takes the screen, changes the display
mode and holds the palette in full screen, and there are no DirectDraw
surfaces. There is no text output; the only font set is the Windows menu font,
changed in full screen so that the menu bar fits in the 20 rows the 480-row
screen leaves above the 460-row drawing area. The drawing area is placed at
the client origin, directly under the menu bar, in both modes. In full screen
the client area is 480 rows less the menu bar, so the bottom rows the menu bar
displaces are the only ones that can fall off. In a window, the width passed
is the adjusted right edge instead of right minus left, which gives a client
640 wide when the fixed frame is 3 pixels; the client height is
`480 + iMenuHeight + 8` plus the bottom frame, less the caption, the frames and
the menu bar, a few rows more than 460 on usual metrics, and those rows show
the black class background. Nothing ever replaces the system colours.

## Alternatives

The client size in a window depends on the frame, caption and menu metrics of
the running system and has not been measured. Whether any display driver
refuses `SetDisplayMode` at 16 bits, which makes the game fall back to 8, has
not been observed.

## How to reproduce

The window is created at `0x00425AD0` (windowed) and `0x00425A3F` (full
screen); the DirectDraw calls are through the object's table at `0x00425B35`,
`0x00425B56` and `0x00425BBD`; `SPI_SETNONCLIENTMETRICS` is at `0x00425C8A`.
Search the file for the little-endian bytes of each entry address listed under
No callers.
