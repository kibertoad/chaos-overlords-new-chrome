---
id: FND-GANG-010
title: The compact gang panel places each value field in its 320-pixel frame, closes only on its face, Enter or Execute, and covers the base values with a black pattern
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00455B6B..0x00456F7B
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_00455B6B` (range in FND-EXE-004) takes a 32-byte gang record by value
(FMT-STATE-001) and a flag byte after it, which every caller sets to 1
(FND-GANG-009). It loads resource 5022 into surface 7 at
`(top=144, left=344, bottom=353, right=688)` (`0x00455BC9`). The portrait,
surface 7 `(370,161)`, lands at screen `(154,141)`, so for this panel surface
7 `(x, y)` is screen `(x - 216, y - 20)`; that mapping gives the screen
positions below.

| Field | Drawn by | Surface 7 | Screen |
|---|---|---|---|
| Definition name | `fn_00413FD5` | `(444,171)` | `(228,151)` |
| Description, three rows of 30 characters | `fn_00413FD5` | `(444,189)`, `(444,198)`, `(444,207)` | `(228,169)`, `(228,178)`, `(228,187)` |
| `force` (record byte 3), or the text at `0x0048776C` when it is 0 | `fn_00414187`, 2 cells; `fn_00413FD5` | `(516,236)` | `(300,216)` |
| Upkeep, the definition word at offset `0x7C` negated | `fn_00414187`, 2 cells | `(612,236)` | `(396,216)` |
| Tech Level, definition offset `0x82` | `fn_00414187`, 2 cells | `(612,245)` | `(396,225)` |
| `combat`, `defense` (record `0x12`, `0x13`) | `fn_00414187`, 2 cells | `(516,263)`, `(516,272)` | `(300,243)`, `(300,252)` |
| `stealth`, `detect` (`0x14`, `0x15`) | `fn_00414187`, 2 cells | `(612,263)`, `(612,272)` | `(396,243)`, `(396,252)` |
| `chaos`, `control`, `heal`, `influence`, `research` (`0x16` to `0x1A`) | `fn_004142E7`, 2 cells | x 516, y 290, 299, 308, 317, 326 | x 300, y 270, 279, 288, 297, 306 |
| `strength`, `blade`, `ranged`, `fighting`, `martial_arts` (`0x1B` to `0x1F`) | `fn_004142E7`, 2 cells | x 612, same rows | x 396, same rows |

When `pref_base_stats` (`0x0048784C`) is set (`0x004561DE`), it draws the
definition's own values with `fn_00414187` in 2 cells, 18 pixels left of each
effective value: offsets `0x7E` and `0x80` at surface x 498 on rows 263 and
272, `0x84` and `0x86` at x 594 on the same rows, `0x88` to `0x90` at x 498
and `0x92` to `0x9A` at x 594 on rows 290 to 326. It then sets the pattern
selector at `0x00494868` through `fn_00449B20` from the grey value `0x7F`
(which gives 0) and draws black through `fn_004266A6` in its pattern mode over
four areas: surface 7 `(498,263)-(510,281)`, `(594,263)-(606,281)`,
`(498,290)-(510,335)` and `(594,290)-(606,335)` (`0x00456740` to
`0x00456935`), screen `(282,243)-(294,261)`, `(378,243)-(390,261)`,
`(282,270)-(294,315)` and `(378,270)-(390,315)`. The full panel
`fn_00449E80` draws its base values without this pattern (FND-GANG-006).

It slides the panel in with `fn_0041953E(1)`, the 320-pixel form, and reads
events through `fn_00462579`:

- Key down (`0x004569A6`): Enter (`0x0D`) or Execute (`0x2B`) presses the face
  `(top=293, left=161, bottom=316, right=211)` with `fn_00418CCC(0, ...)` and
  closes the panel. Escape and every other key are ignored.
- Left button down and left double-click alike (`0x00456A25`, `0x00456B6A`):
  outside `(128,124)-(448,333)` they play slot 4. Inside, in coordinates
  relative to `(128,124)`, the face at `(33,169)-(82,191)` closes the panel
  when `fn_00418821(0, ...)` reports a release inside. Nothing else reacts.
- Paint (`0x00456CB5`): restores the screen from surface 1 and the panel from
  surface 7 `(344,144)-(664,353)` to screen `(128,124)-(448,333)`; with the
  flag byte set it also copies surface 7 `(0,144)-(24,353)` to screen
  `(104,124)-(128,333)`, the left edge of the order panel it was opened from.

On exit it slides the panel out with `fn_004196F5(1, flag)`, restores the
screen with `fn_004120CB` only when the flag is 0, and sets the byte at
`0x00498100` to 1.

## Interpretation

The compact panel lays out the same fields as the full Gang Information panel
of FND-GANG-006, 24 pixels further right: values at x 300 and 396, base values
at x 282 and 378, on the same rows. The close face sits 24 pixels right of the
shared panel's faces, at screen `(161,293)`. Enter or Execute closes it; Escape
does not. With base statistics shown, the base values are drawn and then
covered with a black pattern, so they appear dimmed.

## Alternatives

- The pattern `fn_004266A6` draws for selector 0 has not been read; that it
  dims the digits rather than hiding them is inferred from the digits being
  drawn first.
- The areas stop one pixel short of the last base-value cell's right edge at
  x 510 and 606, 12 pixels from x 498 and 594; whether that covers both digit
  cells fully depends on the cell width of `fn_00414187`, which is not checked
  here.

## How to reproduce

In `0x00455B6B`, find the load of resource `0x139E`, the points passed to
`0x00425F8C` with x `0x1BC`, `0x204`, `0x264`, `0x1F2` and `0x252`, the test
of `0x0048784C`, the call to `0x00449B20` with `0x7FFF` and the four calls to
`0x004266A6`, the key test of `0x2B` and `0x0D` with no test of `0x1B`, the
panel rectangle `(0x7C,0x80,0x14D,0x1C0)` and the face `(0x125,0xA1,0x13C,0xD3)`.
