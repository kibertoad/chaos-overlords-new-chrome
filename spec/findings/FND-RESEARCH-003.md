---
id: FND-RESEARCH-003
title: The Research list builder lists unresearched items of the chosen category up to the gang type's Tech Level, capped by the sector's research-site level, with each item's remaining research
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004437E7..0x00443BBC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00494900..0x00494AEF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004948A8..0x004948E7
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_004437E7(category, limit, player, roster_slot)` (range in FND-EXE-004)
is called only by the Research handler `fn_004427FA`, at `0x00442985` when the
panel opens and at `0x00443028` when a category cell is chosen. The handler
passes as `limit` the 16-bit field at definition offset `0x82` of the acting
gang's definition (`0x004A2882 + 0x9C * definition`), the field FND-GIVE-001
calls `tech_level`.

1. It fills the sixteen 31-byte text rows at `0x00494900 + 31 * row` with 30
   spaces and a terminating zero, and sets the sixteen entries of the list at
   `0x004948A8 + 4 * row` to -1.
2. It fills the list area of surface 7, `(top=169, left=147, bottom=313,
   right=328)`, with black. Surface 7 holds the panel at `(0,144)`, so the
   area is panel-local `(147,25)-(328,169)`.
3. It reads byte `0x0D` of the gang's sector (the `sector` byte, record offset
   2) only when the sector's `owner` is `player`, and takes 0 otherwise. When
   that byte is 0 and `limit` is above 4 the limit becomes 5; when it is 1 and
   the limit is above 7 the limit becomes 8 (FND-STATE-001 reads the same
   caps).
4. For each of the 64 item records at `0x004A5F08 + 0xA6 * item`, in record
   order, it takes the item's `type` (offset `0x7A`) minus 1 when that is at
   least 1, and lists the item when that value equals `category`, the item's
   `tech_level` (offset `0x80`) is at most the limit, and the player's research
   byte at `0x004A2608 + 6 * item + player` is nonzero.
5. For the `k`-th listed item it draws the item's name (offset 0) with the
   plain font helper `fn_00413FD5` at surface 7 `(148, 170 + 9 * k)`, and the
   research byte as a two-digit number with `fn_00414187` at
   `(316, 170 + 9 * k)`. It copies the first 28 bytes of the name into text row
   `k`, spaces for zero bytes, puts the tens digit in column 28 only when it is
   not 0 and the units digit in column 29, and stores the item number in list
   entry `k`.

The builder does not stop at sixteen entries.

The Research handler draws the panel on the screen at `(104,124)`: on a left
button press it plays slot 4 outside `(104,124)-(448,333)` (`0x00442BF6`) and
otherwise subtracts `(104,124)` from the pointer (`0x00442C3C`) before its
rectangle tests. Its category cells are local `(104, 16 + 36 * n)-(136, 48 +
36 * n)` for `n` 0 to 3 (`0x00442E10` to `0x00442F0F`), the Equip panel's
cells (FND-EQUIP-005); choosing one redraws the frame of FND-EQUIP-009 and
rebuilds the list.

## Interpretation

The Research list shows, for the chosen category, every item the player has
not finished researching whose Tech Level the gang type reaches, with the
research still needed. The sector's research-site level caps that at 5 with no
site, at 8 with a level-1 site, and not at all with a level-2 site; a sector
the player does not own counts as level 0. Rows are nine pixels apart from
panel-local y 26, the baseline FND-EQUIP-005 gives for the pointer test. The
text rows and item list at `0x00494900` and `0x004948A8` are the sixteen fixed
entries the Equip and Research handlers share.

Item types 0 and 1 are listed under category 0.

## Alternatives

- The builder writes past the sixteen entries if more than sixteen items
  qualify. In the shipped `DATA/ITEMS` no type holds more than fifteen records
  (FND-EQUIP-005), so this does not happen with the shipped data.
- What the text rows at `0x00494900` are read for (a tooltip, an accessibility
  copy or another panel) has not been traced.

## How to reproduce

In `0x004437E7`, find the two loops of 16 and 30 that write `0x20` to
`0x00494900`, the fill of `(0xA9,0x93,0x139,0x148)`, the read of
`0x004A08F5` guarded by the owner test at `0x0044394C`, the constants 4, 5, 7
and 8, the three conditions on `0x004A5F88`, the argument and `0x004A2608`,
and the calls to `0x00413FD5` at x `0x94` and `0x00414187` at x `0x13C` with
y `0xAA + 9 * k`.
