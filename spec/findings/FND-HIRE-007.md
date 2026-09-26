---
id: FND-HIRE-007
title: The offer refill rejects a draw only when it equals a slot's current value or the gang just removed, and the same function draws the three offers on the console
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047170D..0x004717A4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004717AB..0x004718DF
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_004716EB` (range in FND-EXE-004) loops over offer slots 0 to 2 of the
player in the global at `0x004ABC84`.

- Refill, `0x0047170D..0x004717A4`. When the slot's offer byte at
  `0x004ABBC0 + player * 3 + slot` is negative (`JGE` at `0x00471722` skips
  0 and positive values), it draws `fn_0045D227(0x59)` and draws again while
  the result equals the signed byte of slot 0 (`0x00471742`), of slot 1
  (`0x00471758`) or of slot 2 (`0x0047176E`) of the same player, or equals the
  negation of the slot's current byte (`0x0047178C`). It stores the accepted
  value at `0x004717A4`. These four comparisons are the whole test: it reads no
  other player's offers, no gang record and no other array.
- Drawing, `0x004717AB..0x004718DF`, runs for every slot, refilled or not. It
  reads the 16-bit field at `+0x1E` of the offered definition (`0x004A281E +
  definition * 0x9C`, `0x004717C8`), and takes the 64-by-64 source rectangle at
  x `(n mod 10) * 64`, y `(n / 10) * 64`. It copies it from surface 3 to
  surface 1 with `fn_0042773E(3, 1, ...)` (`0x00471877`) into the rectangle
  built as `fn_00425EDF(373, 440 + 66 * slot, 437, 504 + 66 * slot)`,
  that is `(top, left, bottom, right)`. It then draws the definition's price
  (`+0x7A`) with the number helper `fn_00414187(1, point, price, 2, 1)` at the
  point `(450 + 66 * slot, 440)` (`0x004718C8`).

## Interpretation

A refilled offer differs from the other offers on show and from the gang that
has just left the slot; the other slots' negative values and the slot's own
negative value can never match a draw of 1 to 89. Nothing stops an offer from
repeating a gang another player is offered or a type the player already
employs.

The offers on the main console are 64-by-64 portraits whose top-left corners
are `(440, 373)`, `(506, 373)` and `(572, 373)`, taken from the portrait
sheet in surface 3 (resource 3000, FND-UI-007) by the definition's number, ten
to a row. Each offer's price is drawn two cells wide below its portrait, with
its left edge at x `450 + 66 * slot` and top at y 440. Since the function runs
at each planning entry, the offers are redrawn there for computer players as
well as humans.

## Alternatives

Which screen surface 1 is, the visible screen or a back buffer copied to it,
was not read here.

## How to reproduce

Read `fn_004716EB` from its entry. The four comparisons that loop back to the
`PUSH 0x59` at `0x00471728` are the rejection test; the constants `0x175`,
`0x1B8`, `0x1B5`, `0x1F8`, `0x42`, `0x1C2` build the rectangles.
