---
id: FND-COMBAT-012
title: Combat Results pages with the left and right arrow keys, closes on Enter or plus but not Escape, and the force selector makes one of the viewer's gangs the focus whose target and attackers are framed
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00451F80..0x00453086
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00453A8D..0x00454250
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function ranges are in FND-EXE-004. Result row entries are two 16-bit gang
indices, the gang and its Attack target or -1 (FND-COMBAT-008). Rectangles are
`(left, top)-(right, bottom)`; local coordinates are relative to the panel's
origin `(104,124)` on screen.

`fn_00451F80(viewer, flag)`:

- The page list (`0x00451FB6..0x00452079`) takes, in ascending order, each
  sector where the viewer's first entry is not -1 or the sector record's byte
  at offset `0x10 + viewer` is nonzero, and where at least one player's first
  entry is not -1. The page index `0x004948F0` is a global; it is set to 0
  only when it holds -1 (`0x00451F9F..0x00451FAC`) and is not compared with
  the new page count. With no page the handler calls `fn_00464290(4)` only
  when `flag` is 0.
- On opening, the focal gang `0x00494BF0` and its target `0x004948E8` are the
  two words of the viewer's first entry on the page (`0x004520FC`,
  `0x00452126`), and the page is drawn with no opponent chosen, which picks the
  first opponent with a result (FND-COMBAT-007).
- Keys (event 2): `0x2B` or `0x0D` draws the pressed look over screen
  `(137,293)-(187,316)` and closes (`0x004521AE..0x0045220E`); `0x25` goes to
  the previous page, or calls `fn_00464290(4)` on page 0
  (`0x00452212..0x00452340`); `0x27` goes to the next page, or calls
  `fn_00464290(4)` on the last (`0x00452348..0x0045247B`). A page change
  makes the viewer's first entry of the new page the focal gang. No other key,
  Escape included, is tested.
- Force selector: a click in local `(101,27)-(189,183)` (`0x00452943`) picks
  slot `(x > 144) + 2 * (y > 78) + 2 * (y > 130)` (`0x00452995..0x004529BE`).
  When the viewer's entry in that slot is not -1 it becomes the focal gang, its
  second word the focal target, and the page is redrawn with the same opponent
  (`0x004529E5..0x00452B09`).
- An opponent portrait is accepted only when that opponent's first entry in
  the sector is not -1 (`0x004525EB..0x00452821`); a change plays slot 3 and
  redraws (`0x00452854..0x0045292D`).

`fn_00453A8D(player, origin)` draws one player's six entries for the page's
sector, entry `k` at `origin + (44 * (k % 2), 52 * (k / 2))`:

- When a focal gang is set it draws, around the cell
  `(x - 2, y - 2)-(x + 42, y + 50)`, an outline in the colour triple
  `(0, 0xFFFF, 0)` for the focal gang (`0x00453B5F..0x00453C0D`) and
  `(0xFFFF, 0, 0)` for the focal gang's target (`0x00453C38..0x00453CE6`).
  For an entry whose second word is the focal gang it draws an outline in
  `(0xFFFF, 0xFFFF, 0)`, or, when that entry is also the focal gang's target,
  copies the art at surface 6 `(468,15)-(512,67)` (`0x00453D15..0x00453E70`).
- It scales the 64 by 64 portrait cell of surface 3 selected by the
  definition in byte 0 of the gang's combat record (`0x00453EAB`) to 40 by 40
  (`0x00453F5E`).
- Below it, at `y + 41` and `y + 45`, it draws two tracks 40 pixels long and 3
  rows tall from surface 6, filled 4 pixels per point from record byte 1
  (`0x00453F94`) and record byte 2 (`0x004540ED`).

## Interpretation

- The keyboard pages with the left and right arrow keys and closes the panel
  with Enter or the plus key; Escape does nothing.
- Selecting a slot with the force selector changes which of the viewer's gangs
  is the focus: that gang is outlined in one colour, the gang it attacked in a
  second colour, and every gang in the sector that attacked it in a third, a
  mutual attack getting a separate marker.
- Each gang's cell shows Force before the phase on the upper track and after
  it on the lower.
- Combat record byte 0 is used as a definition number to pick the portrait.
- A page index left from an earlier opening is used as it is; with fewer pages
  than before it can point past the page list.

## Alternatives

- The colour triples are read as red, green and blue; the order of the colour
  helper's arguments was not traced.
- Which resource surface 3 holds when the panel opens was not traced.

## How to reproduce

In `fn_00451F80`, read the event switch at `0x00453015` for the key constants
`0x2B`, `0x0D`, `0x25` and `0x27`, the rectangle `(0x1B,0x65,0xB7,0xBD)` of the
force selector and the slot arithmetic after it. In `fn_00453A8D`, read the
three outline branches and the loads of record bytes 0, 1 and 2.
