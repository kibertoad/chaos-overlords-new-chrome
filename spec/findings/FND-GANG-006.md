---
id: FND-GANG-006
title: The gang information panel handler takes a whole gang record, lays out effective and base statistics in two columns, and closes on one face
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00449E80..0x0044B698
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_00449E80` occupies `0x00449E80..0x0044B698` (6,127 bytes, FND-EXE-004). It
is called from the gang command handler `fn_00414D8C`, from `fn_004169B3` and
twice from the Hire input handler `fn_00416C75`. It takes a 32-byte gang record
(FMT-STATE-001) by value and returns nothing.

Drawing, into surface 7, which the shared panel maps to the screen with
`screen = (x + 104, y - 20)`:

- It saves the screen (`fn_004120A7`), loads resource 5000 (`PX05000`) at
  `(top=144, left=0, bottom=353, right=344)` and copies the definition's 64-by-64
  portrait to `(26,161)`, screen `(130,141)`.
- For each of `weapon`, `armor` and `misc` that is not -1 it loads the item's
  strip and copies frame 0 to `(288, 161 + 64k)`, screen `(392, 141 + 64k)`.
- The definition's name at `(100,171)` and its three 30-character description
  lines at `(100,189)`, `(100,198)` and `(100,207)`, through `fn_00413FD5`.
- Force: when the record's `force` byte is 0 it draws the text at
  `0x00487754` (two question marks) at `(172,236)`; otherwise the number
  there. Upkeep, the definition word at offset `0x7C` negated, at `(268,236)`,
  and the definition's `tech_level` (offset `0x82`) at `(268,245)`.
- Effective statistics from the record, all two-cell numbers: Combat `(172,263)`,
  Defense `(172,272)`, Stealth `(268,263)` and Detect `(268,272)` through
  `fn_00414187`; Chaos, Control, Heal, Influence and Research at x 172 and
  Strength, Blade, Ranged, Fighting and Martial Arts at x 268, on rows 290,
  299, 308, 317 and 326, through `fn_004142E7`.
- When `pref_base_stats` (`0x0048784C`) is set, the definition's base value of
  each of the same fourteen statistics (definition offsets `0x7E`, `0x80`,
  `0x84` to `0x9A`) goes 18 pixels to the left of the effective value, at
  x 154 and x 250 on the same rows, through `fn_0041B668`.

It then slides the panel in with `fn_0041953E(0)` and reads events through
`fn_00462579`:

- Enter or Execute presses the face `(top=293, left=137, bottom=316, right=187)`
  with `fn_00418CCC(0, ...)` and closes the panel.
- Left button down outside `(104,124)-(448,333)` plays slot 4. Inside, the face
  at panel-local `(33,169)-(82,191)` closes the panel when `fn_00418821(0, ...)`
  reports a release inside. Nothing else reacts to a single press.
- Left double-click takes the same close test, and on an item picture, local
  `(287,16)-(337,66)`, `(287,80)-(337,130)` or `(287,144)-(337,194)`, opens
  the Item Information panel `fn_0044B699` for that item and restarts the
  animation at frame 0.
- Paint restores the screen from surface 1 and the panel from surface 7.
- With no event and the animation timer expired it steps a 15-frame counter
  and copies the current frame of each carried item to the screen at
  `(392, 141 + 64k)`.

The handler writes no game state. On exit it slides the panel out
(`fn_004196F5(0, 0)`), restores the screen (`fn_004120CB`) and sets the byte at
`0x00498100` to 1.

## Interpretation

This is the Gang Information panel of a gang on the map, SCR-GANG-002. On
screen its value fields start at x 276 and x 372 on rows 216 (Force, Upkeep),
225 (Tech Level), 243 and 252 (Combat and Stealth, Defense and Detect) and
270 to 306 in steps of 9 for the other ten statistics; the base values, when
shown, start at x 258 and x 354. A gang whose Force byte is 0 shows two
question marks in place of its Force. The panel's only control is the close
face; double-clicking a carried item opens its information.

## Alternatives

- `fn_0041B668`, which draws the base values, has not been read; it is taken to
  draw numbers the way `fn_00414187` does.
- Which caller passes a record with Force 0 (a hire offer shown through this
  panel, or a gang whose Force was spent) has not been traced.

## How to reproduce

In `0x00449E80`, find the load of resource 5000, the test of the Force byte
against 0 with the string at `0x00487754`, the negation of the definition word
at `0x004A287C`, the test of `0x0048784C` guarding fourteen calls to
`0x0041B668`, and the close face with left `0x21` and top `0xA9`.
