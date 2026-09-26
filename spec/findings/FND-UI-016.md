---
id: FND-UI-016
title: The site-cooperation report stretches a crop of the site portrait with COLORONCOLOR and masks it with pattern 146
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
    address: 0x0044FD6C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004266A6
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00427E60
tool: Ghidra 12.1.3
environment: null
---

## Observation

- Both stretching copy wrappers, `fn_0042773E` and `fn_00427864`, call
  `SetStretchBltMode(destination, 3)` just before `StretchBlt`, at
  `0x004277FD` and `0x00427854` and at `0x004279A2` and `0x004279F9`. Mode 3 is
  `COLORONCOLOR`.
- In the site-cooperation branch of `fn_0044FD6C` the source rectangle starts
  at x 12 and at the site's 64-pixel row of `PX02000` plus 1, and is 94 wide and
  62 high. It is stretched into the 242-by-158 report aperture.
- `fn_004266A6` and `fn_00427E60` then combine the result with the pattern
  bitmap `Chaos Overlords.exe#BITMAP/146`. That 8-by-8 one-bit pattern keeps two
  pixels per row, x 0 and 4 on even rows and x 2 and 6 on odd rows, and turns
  the other 75 percent black.
- Resource 6004 (`PX06004`) is drawn after the mask and is not masked.

## Interpretation

The site-cooperation report shows the middle of the site's portrait, without its
border, enlarged by dropping or repeating pixels, with three pixels in four
blacked out in a fixed pattern. The foreground figure is drawn over it
unmasked.

## Alternatives

None known.

## How to reproduce

Find the `SetStretchBltMode` import calls in `0x0042773E` and `0x00427864`, and
in `0x0044FD6C` the constants 12, 94 and 62 of the site crop, followed by the
call to `0x004266A6`.
