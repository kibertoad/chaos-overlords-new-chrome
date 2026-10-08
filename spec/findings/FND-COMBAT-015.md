---
id: FND-COMBAT-015
title: Detailed Combat draws each gang portrait and its two Force tracks into the same surface, 68 and 75 rows below the portrait's top
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042F0D4..0x0042F18B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042FCA5..0x0042FCB9
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004303E4..0x004303F8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042F794..0x0042F979
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00430687..0x0043080C
tool: Python 3.14.7 script disassembling the file bytes with Capstone 5.0.7
environment: null
---

## Observation

Rectangles are built by `fn_00425EDF(top, left, bottom, right)`. Surface 7 is
the scratch surface the combat panels are composed in at `(0,144)`
(FND-COMBAT-009).

- `fn_0042EE46` reads the gang index from the byte at `0x00494770`, takes the
  gang's portrait number `n`, and copies a 64-by-64 cell of surface 3, placed
  by 64 times the quotient and the remainder of `n` divided by 10, to surface 7
  with the destination rectangle top 192, left 150, bottom 256, right 214
  (`0x0042F0D4..0x0042F18B`).
- `fn_0042F98B` builds the rectangle top 192, left 223, bottom 256, right 287
  for the right gang's portrait at `0x0042FCA5` and again at `0x004303E4`.
- `fn_0042F779` draws the left gang's tracks into surface 7 with the
  rectangles top 260, left 152, bottom 263, right 212 (`0x0042F794`,
  `0x0042F80F`) and top 267, bottom 270 (`0x0042F88F`, `0x0042F90A`).
- `fn_0043066C` draws the right gang's tracks with the same tops and bottoms
  and left 225, right 285 (`0x00430687..0x0043080C`).
- Surface 7 reaches the screen through the one copy of the composed panel
  (FND-COMBAT-009), so the portraits and the tracks are moved by the same
  offset.

## Interpretation

Each gang portrait is drawn at panel-local y 48 and each pair of tracks at
panel-local y 116 and 123: the upper track starts 68 rows below the top of the
portrait and 4 rows below its bottom edge, the lower track 7 rows lower.

The capture of FND-UI-010 places the portraits at panel-local y 48, as this
reading does, and the tracks at y 114 and 121, 66 rows below the portrait's
top. Since both are drawn through the same copy, no offset of the panel or of
the capture can move one by 2 rows and not the other. The capture's track
rows are two rows higher than the code writes them.

## Alternatives

- A capture scaled or filtered on its way to the measurement could blur a
  3-row track into neighbouring rows and move where its edge was read; the
  settings of FND-UI-010's capture were not recorded.

## How to reproduce

In `fn_0042EE46`, find the call to `0x00425EDF` with `0xC0`, `0x96`, `0x100`
and `0xD6` at `0x0042F138` and the copy from surface 3 to surface 7 at
`0x0042F186`. In `fn_0042F98B`, find the calls with `0xC0`, `0xDF`, `0x100`
and `0x11F`. In `fn_0042F779` and
`fn_0043066C`, find the calls with `0x104`, `0x107`, `0x10B` and `0x10E`.
