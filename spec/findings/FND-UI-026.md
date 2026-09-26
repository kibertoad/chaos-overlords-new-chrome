---
id: FND-UI-026
title: Of the 77 calls to the copy wrapper, only the setup card portrait is scaled, and it asks for the pattern mode, so no keyed copy loses its key
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00427864..0x00427A08
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040F4CC
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function extents are those of FND-EXE-004.

- The copy wrapper `fn_00427864(src, dst, src_rect, dst_rect, mode)` compares
  the widths and heights of the two rectangles. When they are equal, mode 0
  calls the pattern compositor `fn_00427E60` (FND-GFX-004), mode 1 the
  white-keyed copy `fn_00427A09`, and any other mode `BitBlt` with `SRCCOPY`.
  When they differ, it uses `StretchBlt` with `SRCCOPY` in `COLORONCOLOR` mode
  and never reads the mode (FND-UI-023).
- It has 77 call sites in 32 functions. Every site pushes a literal mode: 72
  pass 1 and 5 pass 0. No site passes another value.
- The rectangles at each site come from the rectangle helpers `fn_00425EDF`
  (top, left, bottom, right) and `fn_00425F4D` (point, width, height). Where a
  corner depends on a variable, both corners use the same variable plus
  constants, so the size is still a constant. At the four sites where a switch
  picks the destination (`0x004543DC`, `0x0043DAC7`, `0x0043F680`,
  `0x004427E8`), every case gives the size of the source.
- Only one site copies between rectangles of different sizes: `0x0040F4CC` in
  the setup card renderer `fn_0040EE8A`, which asks for mode 0 and scales a
  32-by-30 portrait to 64 by 60 (FND-SETUP-005).

| Call site | Function | Source surface | Destination surface | Mode | Source size | Destination size |
|---|---|---|---|---|---|---|
| `0x00457E1E` | `fn_00457B7B` | 6 | 1 | 0 | 32 by 32 | 32 by 32 |
| `0x00463500` | `fn_00462579` | 6 | 1 | 1 | 54 by 52 | 54 by 52 |
| `0x0046366F` | `fn_00462579` | 6 | 1 | 1 | 54 by 52 | 54 by 52 |
| `0x00413137` | `fn_00413012` | 6 | 1 | 1 | 23 by 13 | 23 by 13 |
| `0x00413218` | `fn_00413012` | 6 | 1 | 1 | 23 by 13 | 23 by 13 |
| `0x004132FB` | `fn_00413012` | 6 | 1 | 1 | 13 by 23 | 13 by 23 |
| `0x004133E1` | `fn_00413012` | 6 | 1 | 1 | 13 by 23 | 13 by 23 |
| `0x00413505` | `fn_00413012` | 6 | 1 | 1 | 13 by 23 | 13 by 23 |
| `0x00413619` | `fn_00413012` | 6 | 1 | 1 | 13 by 23 | 13 by 23 |
| `0x0041372E` | `fn_00413012` | 6 | 1 | 1 | 23 by 13 | 23 by 13 |
| `0x00413846` | `fn_00413012` | 6 | 1 | 1 | 23 by 13 | 23 by 13 |
| `0x0042D9D7` | `fn_0042CE61` | 7 | 1 | 1 | 48 by 48 | 48 by 48 |
| `0x0042E019` | `fn_0042CE61` | 7 | 1 | 1 | 48 by 48 | 48 by 48 |
| `0x00411FAD` | `fn_00411DF5` | 6 | 1 | 1 | 54 by 52 | 54 by 52 |
| `0x004126D4` | `fn_004123CC` | 6 | 2 | 1 | 54 by 52 | 54 by 52 |
| `0x00412789` | `fn_004123CC` | 6 | 2 | 1 | 54 by 52 | 54 by 52 |
| `0x00412A9D` | `fn_004123CC` | 6 | 2 | 1 | 20 by 28 | 20 by 28 |
| `0x00412BE5` | `fn_00412AC4` | 7 | 2 | 1 | 20 by 14 | 20 by 14 |
| `0x00412DFB` | `fn_00412BF7` | 6 | 2 | 1 | 20 by 20 | 20 by 20 |
| `0x00412FF8` | `fn_00412BF7` | 6 | 2 | 1 | 20 by 20 | 20 by 20 |
| `0x00418034` | `fn_00417CBA` | 6 | 1 | 1 | 64 by 64 | 64 by 64 |
| `0x00418459` | `fn_00417CBA` | 6 | 1 | 1 | 64 by 64 | 64 by 64 |
| `0x004116BE` | `fn_00411119` | 6 | 7 | 1 | 162 by 156 | 162 by 156 |
| `0x004117E5` | `fn_00411119` | 6 | 7 | 1 | 13 by 23 | 13 by 23 |
| `0x004118E0` | `fn_00411119` | 6 | 7 | 1 | 23 by 13 | 23 by 13 |
| `0x004119E0` | `fn_00411119` | 6 | 7 | 1 | 13 by 23 | 13 by 23 |
| `0x00411AE3` | `fn_00411119` | 6 | 7 | 1 | 13 by 23 | 13 by 23 |
| `0x00411BE8` | `fn_00411119` | 6 | 7 | 1 | 23 by 13 | 23 by 13 |
| `0x00411CED` | `fn_00411119` | 6 | 7 | 1 | 23 by 13 | 23 by 13 |
| `0x00450422` | `fn_0044FD6C` | 7 | 7 | 1 | 242 by 158 | 242 by 158 |
| `0x00453DC6` | `fn_00453A8D` | 6 | 7 | 1 | 44 by 52 | 44 by 52 |
| `0x004543DC` | `fn_00454251` | 6 | 0 | 1 | 34 by 34 | 34 by 34 |
| `0x00410BAA` | `fn_00410770` | 6 | 7 | 1 | 120 by 64 | 120 by 64 |
| `0x004174DC` | `fn_00416C75` | 6 | 7 | 1 | 40 by 40 | 40 by 40 |
| `0x0041A425` | `fn_0041A0D4` | 6 | 7 | 1 | 162 by 156 | 162 by 156 |
| `0x0041A53E` | `fn_0041A0D4` | 6 | 7 | 1 | 13 by 23 | 13 by 23 |
| `0x0041A639` | `fn_0041A0D4` | 6 | 7 | 1 | 23 by 13 | 23 by 13 |
| `0x0041A739` | `fn_0041A0D4` | 6 | 7 | 1 | 13 by 23 | 13 by 23 |
| `0x0041A83C` | `fn_0041A0D4` | 6 | 7 | 1 | 13 by 23 | 13 by 23 |
| `0x0041A941` | `fn_0041A0D4` | 6 | 7 | 1 | 23 by 13 | 23 by 13 |
| `0x0041AA46` | `fn_0041A0D4` | 6 | 7 | 1 | 23 by 13 | 23 by 13 |
| `0x0041AF8F` | `fn_0041ACE6` | 6 | 7 | 1 | 13 by 23 | 13 by 23 |
| `0x0041B06C` | `fn_0041ACE6` | 6 | 7 | 1 | 13 by 23 | 13 by 23 |
| `0x0041B15F` | `fn_0041ACE6` | 6 | 7 | 1 | 23 by 13 | 23 by 13 |
| `0x0041B264` | `fn_0041ACE6` | 6 | 7 | 1 | 23 by 13 | 23 by 13 |
| `0x00449A91` | `fn_004499A9` | 7 | 7 | 1 | 20 by 14 | 20 by 14 |
| `0x00415DE9` | `fn_00414D8C` | 6 | 7 | 1 | 40 by 40 | 40 by 40 |
| `0x00419CA8` | `fn_00419AA8` | 6 | 7 | 1 | 120 by 64 | 120 by 64 |
| `0x0043D120` | `fn_0043D073` | 6 | 0 | 1 | 48 by 48 | 48 by 48 |
| `0x0043DAC7` | `fn_0043D93C` | 6 | 0 | 1 | 34 by 34 | 34 by 34 |
| `0x0043F680` | `fn_0043F52C` | 6 | 0 | 1 | 34 by 34 | 34 by 34 |
| `0x0043FA6C` | `fn_0043F692` | 5 | 7 | 0 | 120 by 64 | 120 by 64 |
| `0x0043FB13` | `fn_0043F692` | 6 | 7 | 1 | 120 by 64 | 120 by 64 |
| `0x0043FC5C` | `fn_0043F692` | 6 | 7 | 1 | 120 by 64 | 120 by 64 |
| `0x0043FD84` | `fn_0043F692` | 6 | 7 | 1 | 120 by 64 | 120 by 64 |
| `0x0043FF23` | `fn_0043F692` | 5 | 7 | 0 | 120 by 64 | 120 by 64 |
| `0x0043FFCD` | `fn_0043F692` | 6 | 7 | 1 | 120 by 64 | 120 by 64 |
| `0x0044011C` | `fn_0043F692` | 6 | 7 | 1 | 120 by 64 | 120 by 64 |
| `0x0044024A` | `fn_0043F692` | 6 | 7 | 1 | 120 by 64 | 120 by 64 |
| `0x004403E3` | `fn_0043F692` | 5 | 7 | 0 | 120 by 64 | 120 by 64 |
| `0x0044048A` | `fn_0043F692` | 6 | 7 | 1 | 120 by 64 | 120 by 64 |
| `0x004405D3` | `fn_0043F692` | 6 | 7 | 1 | 120 by 64 | 120 by 64 |
| `0x00440707` | `fn_0043F692` | 6 | 7 | 1 | 120 by 64 | 120 by 64 |
| `0x004427E8` | `fn_004425AE` | 6 | 0 | 1 | 32 by 32 | 32 by 32 |
| `0x00445722` | `fn_00445655` | 6 | 0 | 1 | 192 by 54 | 192 by 54 |
| `0x0044586E` | `fn_00445655` | 6 | 0 | 1 | 192 by 54 | 192 by 54 |
| `0x004459BA` | `fn_00445655` | 6 | 0 | 1 | 192 by 54 | 192 by 54 |
| `0x00447B9F` | `fn_00447ADB` | 6 | 0 | 1 | 54 by 54 | 54 by 54 |
| `0x00447CDC` | `fn_00447ADB` | 6 | 0 | 1 | 54 by 54 | 54 by 54 |
| `0x00447E19` | `fn_00447ADB` | 6 | 0 | 1 | 54 by 54 | 54 by 54 |
| `0x00448015` | `fn_00447ADB` | 6 | 0 | 1 | 32 by 32 | 32 by 32 |
| `0x0044C660` | `fn_0044C476` | 6 | 7 | 1 | 120 by 64 | 120 by 64 |
| `0x0040CA34` | `fn_0040C4C5` | 7 | 7 | 1 | 64 by 62 | 64 by 62 |
| `0x004696AC` | `fn_0046913D` | 7 | 7 | 1 | 64 by 62 | 64 by 62 |
| `0x0040F3F6` | `fn_0040EE8A` | 7 | 7 | 1 | 64 by 62 | 64 by 62 |
| `0x0040F4CC` | `fn_0040EE8A` | 6 | 7 | 0 | 32 by 30 | 64 by 60 |
| `0x0040F9BC` | `fn_0040F72E` | 6 | 7 | 1 | 40 by 40 | 40 by 40 |

## Interpretation

No white-keyed copy in the build is scaled, so the opaque scaling path never
removes a key from a visible draw. The one scaled copy is the portrait of a
setup card that is not selected, which asks for the pattern compositor and is
drawn opaque instead, as FND-SETUP-005 records.

## Alternatives

- Sizes were worked out from the constants passed to the rectangle helpers; a
  helper that adjusted a size at run time would change the result. Neither
  helper does, by FND-GFX-004.

## How to reproduce

List the call sites of `0x00427864`. At each, read the constant pushed as the
last argument and the constants passed to `0x00425EDF` or `0x00425F4D` for the
two rectangle arguments, and compare the widths and heights.
