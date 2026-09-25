---
id: FND-DATA-004
title: DATA/CLT00002 is 236 four-byte entries whose fourth byte is always 4
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: DATA/CLT00002
    offset: 0x00..0x3B0
tool: Python 3.14.7 script reading the file bytes
environment: null
---

## Observation

- The file is 944 bytes, 236 entries of 4 bytes.
- The fourth byte of every entry is 4.
- The first three bytes of every entry are multiples of 17 (`0x11`), and 202
  of the 236 entries use only 0, 51, 102, 153, 204 and 255, the six levels of
  a 6 by 6 by 6 colour cube. The file's first entries are `FF FF 99`,
  `FF FF 66`, `FF FF 33`, `FF CC FF`. The last 16 are ramps of the other
  multiples of 17: nine with only the third byte set, from `00 00 EE` down to
  `00 00 11`, then seven greys from `EE EE EE` down to `11 11 11`.
- The `PX08` images do not carry this list as their palette: their palettes
  hold the same kind of colour cube in another order.

## Interpretation

The file is a list of 236 `PALETTEENTRY` values (red, green, blue, flags). 4
is the Windows flag `PC_NOCOLLAPSE`. 236 is the number of palette entries
between the 10 colours Windows reserves at each end of a 256-colour palette,
which is where the palette loader puts the CLT file's colours
(FND-PLATFORM-007).

## Alternatives

The byte order could be blue, green, red. The grey ramp at the end would read
the same either way; the red-green-blue reading is the one `PALETTEENTRY`
uses.

## How to reproduce

Read `DATA/CLT00002` in 4-byte steps and count the values of each byte.
