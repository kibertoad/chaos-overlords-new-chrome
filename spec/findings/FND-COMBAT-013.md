---
id: FND-COMBAT-013
title: Surfaces 3 and 5 hold PX03000 and PX02000 with PX04999 whenever a combat panel is open, the outline colours are red, green and blue in that order, and the Detailed Combat fight list has room for 36 elements
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00418F16..0x00419021
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00464DF6
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046501C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00425E99..0x00425EDE
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00426575..0x004265FE
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00453B73..0x00453B83
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043087E..0x00430C22
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004945A8..0x0049470F
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function ranges are in FND-EXE-004; the files each load names are those of
FND-GFX-005.

Surfaces 3 and 5. The image loader `fn_00464108(surface, image, ...)` has 97
call sites, and every one passes the surface as an immediate value. Five pass
surface 3 or 5:

- `fn_00418F16`, called once from `fn_0046E766` at `0x0046EB1B`, loads image
  3000 into surface 3 (`0x00418F95`), image 2000 into surface 5
  (`0x00418FD1`) and image 4999 into surface 5 at x 120 (`0x00419010`).
- `fn_00464D53`, the About screen (FND-UI-007), loads image 100 into surface 3
  (`0x00464DF6`) and loads image 3000 into it again before it returns
  (`0x0046501C`).

The Attack picker copies an equipment icon from surface 5 at x 120 (`0x78`,
stored at `0x0043B4D1`) and y `20 * id` (`0x0043B4BF..0x0043B4CA`), 20 by 20.

Colour order. `fn_00425E99(out, a, b, c)` stores bits 8 to 15 of `a`, `b` and
`c` as the first, second and third 16-bit words of the colour
(`0x00425EA2..0x00425EBC`). `fn_00426575` builds the value it passes to
`CreatePen` (`0x004265BB`) and `CreateSolidBrush` (`0x004265FE`) as the first
word's low byte, plus the second word's low byte shifted left by 8, plus the
third word's low byte shifted left by 16 (`0x0042659D..0x004265B4`). The
Combat Results focal outline pushes `a` 0, `b` 0xFFFF and `c` 0
(`0x00453B73..0x00453B7A`).

Fight list area. The list builder `fn_0043087E` stores each element's 10-byte
record at `0x004945A8 + 10 * n` (`0x00430AD0`, and the police element's
stores from `0x00430B66`), its element number at `0x00494780 + 2 * n`
(`0x00430B0E`) and its target at `0x00494718 + 2 * n` (`0x00430B3B`), where
`n` is the running count, with no test of `n` against a limit. It sets the 36
words at `0x00494780` to -1 first. No instruction in the program refers to an
address in `0x004945D0..0x0049470F`; the next global after the record area is
the list length at `0x00494710`, and the next after the target area is the
byte `0x00494760`.

## Interpretation

- Surface 3 holds `PX03000`, the portrait sheet, from startup on; the About
  screen swaps it out and puts it back before returning. Surface 5 holds
  `PX02000` in x 0 to 119 and `PX04999`, the item icons, from x 120. So every
  combat panel draws its portraits from `PX03000` and the picker its item
  icons from `PX04999`.
- The colour words are red, green and blue in that order, the order of a
  Windows `COLORREF`. The Combat Results outlines are green for the focal
  gang, red for its target and yellow for its attackers.
- The fight list has room for 36 records, 36 element numbers and 36 targets.
  A list holds the focal gang, its target, every gang in the sector that
  attacked the focal gang and is not its target, and the police. The picker
  offers only other players' gangs, and a sector's row holds at most six
  entries per player, so at most 30 gangs attack one gang and a list has at
  most 33 elements. Only a list of 37 or more would write past the record area
  into the list length at `0x00494710`.

## Alternatives

- The surfaces were traced through the image loader only. A copy into surface
  3 or 5 by a blit routine was not searched for.
- Whether a computer player can order an Attack on one of its own gangs, which
  could lengthen a list, was not checked; with all five of a player's other
  gangs in the sector attacking the sixth the list would reach 37.

## How to reproduce

List the call sites of `0x00464108` with the pushes before each. Read
`0x00425E99` and the start of `0x00426575` up to the `CreatePen` call, and
the pushes at `0x00453B73`. In `0x0043087E`, read the indexed stores at
`0x00430AD0`, `0x00430B0E` and `0x00430B3B`, and list the references to
`0x004945D0..0x0049470F`.
