---
id: FND-PLATFORM-002
title: Startup picks the 8-bit or 16-bit image set, and the image loader supplies width and height itself
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00460CCF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00464155
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004273D5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00426F77
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048789C..0x004878C3
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487500..0x00487513
tool: Ghidra 12.1.3
environment: null
---

## Observation

- Startup function `0x00460CCF` probes two fixed paths, the strings at
  `0x0048789C` and `0x004878B0`, which read ` data\PX08\px00128` and
  ` data\PX16\px00128` (each with a leading space). The result sets a global
  display depth of 8 or 16 bits before any other image is loaded.
- The numeric image loader `0x00464155` starts from the template
  `data\PX08\PX00000` (string at `0x00487500`), replaces `08` with `16` when
  the depth is 16, writes the requested resource number as five digits in
  place of `00000`, and passes the path with a width and height given by its
  caller to `0x004273D5`.
- `0x004273D5` reads the 14-byte file header and the 40-byte information
  header, overwrites width and height with the caller's values and the plane
  count with 1, keeps the file's bit count and compression, and reads a colour
  table only when the bit count is below 16. It uploads the pixels with
  `SetDIBits`, using colour-use value 0 (`DIB_RGB_COLORS`) for 8-bit data and
  1 (`DIB_PAL_COLORS`) for 16-bit data, and then copies the temporary bitmap
  to the surface the caller asked for.
- The older loader `0x00426F77`, which takes either an executable resource or
  a file, repairs the header and uploads the pixels in the same order.

## Interpretation

The game uses the `PX16` set when `DATA/PX16/PX00128` can be opened at startup
and the `PX08` set otherwise, and never mixes them. The image files do not
carry their own width and height: the code that asks for an image knows its
size. The width, height and plane fields in the files are overwritten before
the pixels are used, whatever they hold (FND-GFX-001, FND-GFX-002). `DIB_PAL_COLORS` has no effect on a 16-bit
direct-colour bitmap, so the 16-bit pixels reach the surface unchanged.

## Alternatives

The exact test that decides between 8 and 16 bits (whether it also asks the
display for its depth, and what happens when both probes fail) has not been
written down. The five-digit formatting has not been matched to a specific
formatter call.

## How to reproduce

Find the references to the strings at `0x0048789C` and `0x004878B0`; they lead
to `0x00460CCF`. The template at `0x00487500` leads to `0x00464155`, which
calls `0x004273D5`.
