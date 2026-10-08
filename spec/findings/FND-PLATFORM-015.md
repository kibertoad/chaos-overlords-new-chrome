---
id: FND-PLATFORM-015
title: The keyed compositor sets its depth's key with one SetBkColor call and restores the colour with a second, and 72 of the 77 calls of the copy wrapper ask for it
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042773E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00427864
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00427A09
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00427A16..0x00427A36
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00427C84
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00427CB8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00427E60
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040E105
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004128A8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045037B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042F98B
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function extents are those of FND-EXE-004. A rectangle argument is built by
`fn_00425EDF(top, left, bottom, right)`; rectangles below are written
`(left,top)` with a width and height. Surface 6 holds `PX00129` (FND-UI-031).

- The copy `fn_0042773E` uses the raster operation `SRCCOPY`. When the source
  and destination sizes differ it first sets the stretch mode `COLORONCOLOR`
  and copies with `StretchBlt`, opaquely.
- The wrapper `fn_00427864(source, destination, source_rect, destination_rect,
  mode)` copies an unscaled rectangle through the pattern compositor
  `fn_00427E60` for mode 0, through the keyed compositor `fn_00427A09` for mode
  1, and with `BitBlt` and `SRCCOPY` for any other mode. When the sizes differ
  it sets `COLORONCOLOR` and copies with `StretchBlt` and `SRCCOPY`, whatever
  the mode.
- `fn_00427A09` compares the display depth `0x0048787C` with 16 at
  `0x00427A16`. At 16 it stores `0x00FFFCFF`, `RGB(255,252,255)`, into its local
  `[EBP-8]` with the instruction at `0x00427A23`, whose four-byte immediate is at
  `0x00427A26`; at any other depth it stores `0x00FFFFFF`, `RGB(255,255,255)`,
  at `0x00427A2F`. It copies the source rectangle into a colour bitmap, passes
  that local to `SetBkColor` for the bitmap's device context at `0x00427C84`,
  copies the colour bitmap into a one-bit bitmap, and at `0x00427CB8` passes the
  colour the first call returned to `SetBkColor` again. Its masking copies
  follow, with the raster operations `0x330008`, `0x8800C6` and `0x660046`.
- `SetBkColor` (import slot `0x004AE5CC`) has exactly these two calls in the
  executable. No `TransparentBlt`, `MaskBlt` or `AlphaBlend` is imported
  (FND-PLATFORM-001).
- All 77 direct calls of `fn_00427864` push a literal source surface,
  destination surface and mode. 72 ask for mode 1 and 5 for mode 0:
  - Mode 1 from surface 6: 64 calls in 26 functions, the copies FND-UI-031
    lists.
  - Mode 1 from surface 7: `0x00412BE5` in `fn_00412AC4` (the site markers;
    `fn_004123CC` loads resource 150, `PX00150`, into surface 7 at
    `0x004128A8`), `0x00450422` in `fn_0044FD6C` (the Last Turn illustration;
    resource 6004, `PX06004`, is loaded into surface 7 at `0x0045037B`),
    `0x0040F3F6` in `fn_0040EE8A`, `0x0040CA34` in `fn_0040C4C5` and
    `0x004696AC` in `fn_0046913D` (each the 64-by-62 rectangle `(220,138)` of
    surface 7 to surface 7), `0x00449A91` in `fn_004499A9`, and `0x0042D9D7` and
    `0x0042E019` in the awards renderer `fn_0042CE61`, the award icons of
    resource 201 (FND-AWARDS-004).
  - Mode 0: `0x00457E1E` in `fn_00457B7B` and `0x0040F4CC` in `fn_0040EE8A`
    from surface 6, and `0x0043FA6C`, `0x0043FF23` and `0x004403E3` in
    `fn_0043F692` from surface 5.
- The local setup handler `fn_0040E0A0` loads resource 140, `PX00140`, into
  surface 7 with `fn_00464108(7, 140, ...)` at `0x0040E105`.
- `PX00300`, the `PX04xxx` rotation strips and the compact item and art sheets
  are loaded into scratch surfaces and copied with `fn_0042773E`, black pixels
  included. The city renderer `fn_004123CC` loads the ownership layers and
  `PX00150`, and neither reads the police duration nor loads `PX00300`. The one
  load of `PX00300` by constant is in the combat compositor `fn_0042F98B`, which
  copies its police cells opaquely.

## Interpretation

The first `SetBkColor` call sets the key for the depth: `RGB(255,252,255)` at
16-bit depth and `RGB(255,255,255)` at any other. With that background colour,
the copy into the one-bit bitmap sets exactly the pixels equal to the key, and
the masking copies keep the destination there and take the source everywhere
else. The second call puts back the colour the device context had, which is
why a 16-bit run sees it pass `RGB(255,255,255)` (FND-PLATFORM-014); it does
not set a key. At 16-bit depth `RGB(255,252,255)` is how GDI names the RGB555
value `0x7FFF`, the brightest white a 5-bit channel holds, so both depths key
out the same image colour.

Most drawing through the wrapper is keyed. Besides the sheet cells of
FND-UI-031 it draws the site markers, one Last Turn illustration, the setup
screen's arrow overlay from `PX00140`, the award icons, and the copies from
surface 7 whose images this finding does not name. The mode 0 request at
`0x0040F4CC` scales a 32-by-30 source to 64 by 60, so it is copied opaquely.
Black is not a transparent colour anywhere. The city and sector views draw no
police car for a Crackdown.

Because the 16-bit key is an immediate operand, writing `0x00FFFFFF` over the
four bytes at `0x00427A26` once changes the key of every later keyed copy at
16-bit depth.

## Alternatives

- Which images surface 7 holds for the copies in `fn_004499A9`, `fn_0040C4C5`
  and `fn_0046913D` was not traced. `PX00140` is also loaded at
  `0x0040BA9B`, `0x0045724A` and `0x00467ABB` (FND-GFX-005); whether those loads
  feed the copies of `(220,138)` in `fn_0040C4C5` and `fn_0046913D` was not
  read.
- How a display driver converts the 16-bit key at the boundary between 5-bit
  and 8-bit channels is decided at run time, and has not been observed.

## How to reproduce

Find the two references to import slot `0x004AE5CC`; both are inside
`0x00427A09`. Read the depth test at `0x00427A16` and the two stores into
`[EBP-8]` at `0x00427A23` and `0x00427A2F`. List the direct calls of
`0x00427864` and read the source surface, destination surface and mode each
pushes, the last push before each call being the source surface and the
seventh the mode. Read the pushes before the calls of `0x00464108` at
`0x0040E105`, `0x004128A8` and `0x0045037B`.
