---
id: FND-PLATFORM-011
title: At 8 bits the palette comes from data/CLT00002, read as red, green, blue, and PX08 pictures are mapped to it through their own colour tables
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004282AA..0x0042885A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487514..0x00487522
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042885B..0x00428E69
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00427635..0x0042769F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00425850
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges of the functions named here are in FND-EXE-004.

`fn_004282AA(surface, number)` is the only function that loads a palette. It
has four calls, `0x00460F87` and `0x00461574` in `WinMain`, `0x004261F0` in
`fn_00425FB0` and `0x00462968` in the event function `fn_00462579`, and each
passes the number 2.

- It does nothing unless the surface number is 0 to 11, the surface record
  (`0x2C` bytes from `0x00493658`) is in use and the record's depth at
  `0x00493674` is 8 or less (`0x0042833B`).
- It copies the name template `data\CLT00000` (`0x00487514`) to the stack and
  writes the number into the five digits, so the name is `data\CLT00002`,
  relative to the current directory, which the image loader sets to the
  install directory (FND-PLATFORM-010).
- It opens the file with `CreateFileA` for reading (`0x004283D0`) and reads
  1,024 bytes (`0x00428474` or `0x004285CA`) into a block allocated zeroed
  (`GlobalAlloc` flags `0x42`). The handle is not tested, so a missing file
  leaves the block zero and entries 10 to 245 black.
- It makes a one-entry GDI palette (`0x00428433`) and resizes it to 256
  entries. For surface 0 it first saves the system palette into
  `0x00493A30` with `GetSystemPaletteEntries`, sets entries 0 to 9 and 246 to
  255 to the index with flag 2 (`PC_EXPLICIT`) and calls
  `SetSystemPaletteUse` with 1 (`0x004285A9`). For any other surface those
  entries are copied from `0x00493A30` with flag 0.
- Palette entry `i` from 10 to 245 takes red, green and blue from bytes 0, 1
  and 2 of the file's four-byte entry `i - 10` (`0x004284F6`), with flag 4
  (`PC_NOCOLLAPSE`); byte 3 is not used. The 944-byte file holds exactly those
  236 entries, and the other 80 bytes read stay zero and are not used.
- It selects the palette into the surface's device context, deleting the one
  it had, and realizes it. When the DirectDraw object at `0x004874E8` exists it
  calls the object's `CreatePalette` method (offset `0x14`) with flags 8 and
  the result pointer `0x004874E4` (`0x004287F9`), and after that the
  surface's `SetPalette` method (offset `0x7C`, `0x0042881C`).

`fn_0042885B` (another palette file loader), `fn_004289D3` and
`fn_00428ADC` (select and realize a palette) and `fn_00428BAB` (reads the
device's palette capabilities with `GetDeviceCaps`) have no caller, and no
four-byte value in the file equals any of their addresses.

The image loader `fn_004273D5` uploads the pixels with `SetDIBits` using the
file's own header: for a bitmap of fewer than 16 bits per pixel with
`DIB_RGB_COLORS` (`0x00427664`), so the colour table stored in each `PX08`
file is used, and for 16 bits with the value 1 (`0x0042768F`).

Depth. The setup `fn_00425850` reads the depth for the window mode flag
`0x00498354`: in a window it takes `GetDeviceCaps(BITSPIXEL)` of the screen
and keeps it within 8 to 16; full screen it calls `DirectDrawCreate`,
`SetCooperativeLevel` with `0x15` and `SetDisplayMode` with the requested
depth, retrying with 8 when 16 fails, and returns 0 when DirectDraw cannot
be created. What `WinMain` does with the result is in FND-PLATFORM-009: any
depth other than 8 or 16 skips the match.

## Interpretation

`DATA/CLT00002` is the only palette, and it is used only when the display runs
at 256 colours. Its four-byte entries are red, green, blue and an unused
byte. The game keeps the twenty Windows static colours and fills the other
236 from the file. A `PX08` picture is drawn by GDI's colour matching of its
own colour table against that palette, so a picture whose table differs from
`CLT00002` still shows its own colours as closely as the palette allows. At 16
bits no palette is loaded. This settles the two questions FND-PLATFORM-007
left open, and corrects its flags: the entries read from the file carry
`PC_NOCOLLAPSE` only, and `PC_EXPLICIT` goes to the twenty reserved entries,
for surface 0 only. The second copy of the template, at `0x00487524`, belongs
to the unused `fn_0042885B`.

## Alternatives

The DirectDraw palette flags 8 (`DDPCAPS_INITIALIZE` alone, without
`DDPCAPS_8BIT`) look likely to make `CreatePalette` fail; whether full-screen
8-bit play then shows the right colours is for a run of the original. The
exact colours GDI picks for a `PX08` table are not computed here.

## How to reproduce

List the calls of `fn_004282AA` and read the number each pushes. Follow the
entry loops after `ResizePalette` in its two branches, and the two `SetDIBits`
calls in `fn_004273D5` after the test of the header's bit count at
`0x0042763E`.
