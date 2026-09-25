---
id: FND-SETUP-016
title: The handoff card is drawn at 266,130 with the next player's colour, name and portrait, and only its Ready button or a menu command closes it
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004396C0..0x00439F79
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00439F7A..0x0043A1CB
tool: Ghidra 12.1.3
environment: null
---

## Observation

Rectangles are written `(x, y, width, height)` on the screen, whose drawing
surface starts at y 30 here.

The presenter `0x004396C0` takes a player slot. It fills `(0, 0, 640, 400)` of
surface 7 with black, loads resource `0x84` (132) into surface 7 at
`(266, 100, 108, 164)`, fills `(283, 125, 8, 72)` with the slot's colour and
`(293, 125, 60, 7)` with black, draws the slot's name at `(293, 125)` and
copies the portrait, source `(32 * portrait, 480, 32, 32)` of surface 6,
scaled to `(293, 133, 64, 64)`. It then fills `(0, 0, 640, 30)` of the screen
with black, copies all of surface 7 to `(0, 30, 640, 400)`, and fills
`(0, 430, 640, 30)` with black.

On screen the card is therefore at `(266, 130, 108, 164)`, the colour bar at
`(283, 155, 8, 72)`, the name at `(293, 155)` and the portrait at
`(293, 163, 64, 64)`.

A button press is made relative to `(266, 130)` and tested against
`(4, 111, 100, 48)`, the screen rectangle `(270, 241, 100, 48)`. Inside it,
`0x00439F7A` draws the pressed image, source `(388, 512, 100, 48)` of surface 6,
at `(270, 241)`, calls `0x00464290` with 2, and follows the held button,
restoring the released image from surface 7 when the pointer leaves and the
pressed one when it returns; it reports whether the release was inside. A
release inside ends the card.

The loop also handles menu commands `0x81`/4, `0x81`/9 and `0x81`/3 (the first
two can set `0x004ABC90` or the quit byte `0x00487828` after the confirmation
helpers, which ends the card too) and repaints on event 7. It has no key
handling of its own. On leaving it sets `0x00487830` to 1.

## Interpretation

The card covers the whole screen in black apart from the 108-by-164 card,
with the next player's colour stripe, name and 64-by-64 face. Ready is a
100-by-48 button near the card's bottom edge, and no key completes it.

## Alternatives

Surface 6 is taken to be the interface sheet `DATA/PX16/PX00129`. Which menu
items commands `0x81`/3 and `0x81`/4 are is not recorded here.

## How to reproduce

Open `0x004396C0` in Ghidra: the resource load at `0x004397B4`, the fills and
the name and portrait draws up to `0x00439987`, the three screen copies up to
`0x00439B47`, and the press test at `0x00439BE2`. Open `0x00439F7A` for the
pressed image.
