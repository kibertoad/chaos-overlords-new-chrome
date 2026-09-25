---
id: FND-EQUIP-009
title: The Equip and Research panels frame the chosen category cell with a 34-by-34 keyed cell of PX00129 and open on category 0 or the category of the pending order
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043F52C..0x0043F691
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043DE42..0x0043DEB1
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00442947..0x004429B6
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_0043F52C(category)` (range in FND-EXE-004) has 13 call sites: 8 in the
Equip handler `fn_0043DAD9` and 5 in the Research handler `fn_004427FA`. It
does nothing for -1. For a category `n` from 0 to 3 it copies the 34-by-34
cell `(120,171)` of surface 6 (`PX00129`, FND-UI-031) with the keyed mode 1 of
`fn_00427864` (FND-PLATFORM-008) to the screen at `(207, 139 + 36 * n)`. Any
other value leaves the destination rectangle uninitialised.

The category cells of both handlers are the panel-local rectangles
`(104, 16 + 36 * n)-(136, 48 + 36 * n)` (FND-EQUIP-005 for Equip; the Research
handler builds the same four rectangles at `0x00442E10`, `0x00442E64`,
`0x00442EB8` and `0x00442F0F`). With the panel at `(104,124)` that is screen
`(208, 140 + 36 * n)`, so the frame lies one pixel outside the chosen cell.
Before each redraw the handlers restore the column
`(top=139, left=207, bottom=281, right=241)` of the screen from surface 7.

On opening, both handlers start with category 0. When the acting gang's
`action` (record offset 7) is already 5 in the Equip handler (`0x0043DE42`),
or 11 in the Research handler (`0x00442947`), they take the category of the
item in `target` (record offset 8): the 16-bit field at item offset `0x7A`,
minus 1 when that is at least 1. They pass that category and the gang
definition's 16-bit field at definition offset `0x82` (`0x004A2882 + 0x9C *
definition`) to their list builders, `fn_0043F136` for Equip and
`fn_004437E7` for Research (FND-RESEARCH-003), then slide the panel in and
draw the frame.

## Interpretation

The chosen category is marked by a frame drawn over the panel on the screen;
the same art marks the chosen opponent in the Attack picker (FND-ATTACK-002)
and in Combat Results (FND-COMBAT-009). Reopening a panel for a gang that
already has an Equip or Research order shows that order's category. Item
types 0 and 1 both land in cell 0; the Research list builder applies the
same mapping when it chooses the items to list (FND-RESEARCH-003), so with the type values of FMT-DATA-003
the cells list melee and bladed weapons, ranged weapons, armor and
miscellaneous items, in that order.

## Alternatives

- The names of the item types rest on FMT-DATA-003, whose `type` values are
  sourced, and the Equip list builder `fn_0043F136` has not been read for the
  mapping; if either differs, only the numeric mapping 0 and 1 to cell 0, 2 to
  1, 3 to 2 and 4 to 3 stands.

## How to reproduce

In `0x0043F52C`, find the switch with the four destination rectangles of left
`0xCF` and the source rectangle `(0xAB,0x78,0xCD,0x9A)` passed to `0x00427864`
with surfaces 6 and 0 and mode 1. In `0x0043DAD9`, find the comparison of the
copied `action` byte with 5, the load of the word at `0x004A5F82`, and the call
to `0x0043F136` with the word at `0x004A2882`; `0x004427FA` has the same code
with 11 and `0x004437E7`.
