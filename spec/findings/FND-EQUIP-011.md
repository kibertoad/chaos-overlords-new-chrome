---
id: FND-EQUIP-011
title: The Equip list builder ends each row's text with the price, so the chosen row's strip shows it
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043F185
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043F49B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043F4E9
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043F507
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function extents are those of FND-EXE-004. `fn_0043F136` builds the Equip
list. The row texts are 31-byte entries from `0x00494900`, the ones the chosen
row mark `fn_0043EFE5` draws (FND-EQUIP-010).

- For each row it first writes a space to bytes 0 to 29 of the
  entry (`0x0043F185`) and a 0 to byte 30.
- For each listed item it draws the name and the price `p` in the list
  on surface 7 (`fn_00413FD5` at `0x0043F3FC`, `fn_00414187` with the point
  `(316, 9 * row + 170)` at `0x0043F434`), then copies the name's characters into bytes 0 to
  27 of the entry, writing a space at each index from the name's end
  (`0x0043F44B..0x0043F4BC`).
- When `p / 10` is not 0 it writes `p / 10 + 0x30` to byte 28
  (`0x0043F4E9`); it always writes `p % 10 + 0x30` to byte 29
  (`0x0043F507`).

## Interpretation

The 30 characters of the chosen row's strip are the name in the first 28 and
the price in the last two, the tens digit left a space for a price under 10.
The strip's characters 28 and 29 fall at screen x 420 and 426, the two number
cells of the list's price, so the chosen row shows its price in the strip's
font where FND-EQUIP-010 has the strip covering it.

## Alternatives

- A price of 100 or more would write the character after `'9'` to byte 28;
  whether any price reaches it was not read.

## How to reproduce

In `0x0043F136`, read the loop with the compare against `0x1E` that ends at
`0x0043F18D`, the loop with the compare against `0x1C` from `0x0043F44B`, and
the two `IDIV` by 10 with the stores to `0x0049491C` and `0x0049491D` indexed
by the row times 31.
