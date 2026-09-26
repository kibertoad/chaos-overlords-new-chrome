---
id: FND-PLATFORM-008
title: Image copies are opaque except for a pattern mask and an exact-white colour key used by two images
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
    address: 0x004123CC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044FD6C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042F98B
tool: Ghidra 12.1.3
environment: null
---

## Observation

- Copy wrapper `0x0042773E` uses the raster operation `SRCCOPY`. When the
  source and destination sizes differ it first sets the stretch mode
  `COLORONCOLOR` and copies with `StretchBlt`, opaquely.
- Wrapper `0x00427864` takes a mode for unscaled copies: 0 applies a
  pattern mask chosen by the resource, 1 calls the keyed mask compositor
  `0x00427A09`, and any other value copies opaquely with `SRCCOPY`. A scaled
  copy ignores the mode and is always an opaque `COLORONCOLOR` stretch.
- The only `SetBkColor` call in the drawing code (import slot `0x004AE5CC`) is
  inside `0x00427A09`. The key colour it sets is `RGB(255,255,255)` at 8-bit
  depth and `RGB(255,252,255)` at 16-bit depth. No `TransparentBlt`,
  `MaskBlt` or `AlphaBlend` is imported (FND-PLATFORM-001).
- Mode 1 has two constant callers: the city renderer's copy of the site
  markers from `PX00150` at `0x00412AC4`, inside `0x004123CC`, and the Last Turn
  illustration `PX06004` inside `0x0044FD6C`.
- `PX00300`, the `PX04xxx` rotation strips and the compact item and art sheets
  are loaded into scratch surfaces and copied with `0x0042773E`, black pixels
  included.
- The city renderer `0x004123CC` loads the ownership layers and `PX00150`, and
  neither reads the police duration nor loads `PX00300`. The one load of
  `PX00300` by constant is in the combat compositor `0x0042F98B`, which copies
  its police cells opaquely.

## Interpretation

Black is not a transparent colour anywhere in the original. Transparency exists
only for the two mode-1 images, where pixels of maximum white are left out, and
through the pattern masks of mode 0. At 16-bit depth, `RGB(255,252,255)` is how
GDI names the RGB555 value `0x7FFF`, the brightest white a 5-bit channel holds,
so both depths key out the same image colour. The city and sector views draw no
police car for a Crackdown.

## Alternatives

How a display driver converts the 16-bit key at the boundary between 5-bit and
8-bit channels is decided at run time, and has not been observed.

## How to reproduce

Find the single caller of `SetBkColor` through slot `0x004AE5CC`; it is inside
`0x00427A09`. Its callers lead to `0x00427864`, and the constant mode 1 callers
to `0x00412AC4` and `0x0044FD6C`.
