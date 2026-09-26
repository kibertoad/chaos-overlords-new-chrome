---
id: FND-HIRE-009
title: The Hire comparison panel loads resource 5016, draws three 32-by-32 portraits and sixteen value rows per offer, and closes on its one control or Enter
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004546C5..0x0045519C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00471C92
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_004546C5` (range in FND-EXE-004) has one caller, the console dispatcher
`fn_004718EE` at `0x00471C92`. Backing coordinates are those of surface 7;
the panel slides in with the alternate form of `fn_0041953E` (FND-UI-011),
which shows backing x 344 to 664 at screen x 128 to 448 and backing y 144 to
353 at screen y 124 to 333. Panel coordinates below are backing minus
`(344,144)`.

- `0x0045472F`: `fn_00464108(7, 0x1398, ...)` loads resource 5016 into the
  backing rectangle `(top=144, left=344, bottom=353, right=688)`.
- For offer slot `s` = 0, 1, 2 it copies the 156-byte gang definition of the
  player's offer from `0x004A2800 + definition * 0x9C` into a local
  (`0x00454789`), then:
  - copies the 64-by-64 portrait chosen by the definition's field `+0x1E`
    (as in FND-HIRE-007) from surface 3 into the backing rectangle
    `(top=158, left=508 + 40s, bottom=190, right=540 + 40s)`, 32 by 32
    (`0x00454866`);
  - draws sixteen values at backing x `518 + 40s`, each with width 2. The
    first six use `fn_00414187` with last argument 0, the other ten
    `fn_004142E7`:

| Row | Backing y | Value |
|---|---|---|
| 1 | 192 | `+0x82` |
| 2 | 201 | minus `+0x7C` |
| 3 | 211 | `+0x7E` + `+0x92` + `+0x98` + `+0x9A` |
| 4 | 220 | `+0x80` |
| 5 | 229 | `+0x84` |
| 6 | 238 | `+0x86` |
| 7 to 16 | 248, 257, 266, 275, 284, 294, 303, 312, 321, 330 | `+0x88`, `+0x8A`, `+0x8C`, `+0x8E`, `+0x90`, `+0x92`, `+0x94`, `+0x96`, `+0x98`, `+0x9A` |

- Event loop (`0x00454D2F..0x00455173`), with screen coordinates:
  - Key down (type 2) with the value `0x2B` or `0x0D`: it runs the press helper
    `fn_00418CCC(0, ...)` on `(161,293)-(211,316)` and closes. No other key
    is tested.
  - Left button down or double-click (types 3 and 5): outside
    `(104,124)-(448,333)` it plays sound slot 4 (`fn_00464290(4)`,
    `0x00454F59`). Inside, when the point is in `(161,293)-(210,315)` it runs
    the pointer helper `fn_00418821(0, ...)` on `(161,293)-(211,316)` and
    closes when that returns nonzero. No other region is tested.
  - Paint (type 7) restores the screen and the panel.
- Closing slides the panel out with `fn_004196F5(1, 0)`, calls `fn_004120CB`
  and stores 1 in the byte at `0x00498100`.

## Interpretation

The panel is the one FND-HIRE-003 describes. Its portraits sit at screen
`(292 + 40s, 138)` and its value columns at screen x `302 + 40s`, and the rows
lie at screen y 172, 181, 191, 200, 209, 218, then 228 to 310 for the ten
modifier rows, 9 or 10 pixels apart. By the field names of FMT-DATA-002 the
first six rows are Tech Level; Upkeep, drawn as a negative number and so in
the red digit row; a Combat figure that adds the Strength, Fighting and
Martial Arts fields to the Combat field; Defense; Stealth; and Detect. The ten
modifier rows are Chaos, Control, Heal, Influence, Research, Strength, Blade,
Range, Fighting and Martial Arts, in file order.

The only control is the close button at screen `(161,293)` 49 by 22, which
Enter and Execute also press. Escape does nothing here. A click on the city
next to the panel's visible left edge, between x 104 and 128, counts as inside
the panel, since the inside test uses the primary panel rectangle.

## Alternatives

Whether the sum on row 3 is meant as the gang's hand-to-hand strength or is a
slip is not settled by the code.

## How to reproduce

Read `fn_004546C5`: the rectangle constants `0x9E`, `0x1FC`, `0xBE`, `0x21C`
with pitch `0x28`, the point constants `0x206` and `0xC0` to `0x14A`, and the
local record read at `EBP - 0xAC` plus the field offset.
