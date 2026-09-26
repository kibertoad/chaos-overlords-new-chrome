---
id: FND-GIVE-002
title: The Give panel draws each recipient as a card with portrait, Force meter and item icons, covers recipients below the needed Tech Level with a black pattern, and marks selections with keyed PX00129 art
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00448027..0x00448717
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00447ADB..0x00448026
tool: Ghidra 12.1.3
environment: null
---

## Observation

Both functions are called only by the Give handler `fn_00445A4F`
(FND-GIVE-001); ranges are in FND-EXE-004. The handler composes the panel in
surface 7 at `(0,144)`, so surface 7 `(x, y)` is panel-local `(x, y - 144)`
and screen `(104 + x, y - 20)`. Surface 3 holds `PX03000`, surface 5 holds
`PX04999` at x 120 (FND-INFLUENCE-002), surface 6 holds `PX00129`
(FND-UI-031). Copy modes are those of FND-PLATFORM-008.

`fn_00448027(player, tech_needed)`, the recipient list, is called when the
panel opens (`0x00446129`) and when the highest Tech Level of the selected
items changes (`0x00447214`). It works in surface 7 only:

1. It fills the list area with a colour built from the value 48,000 in each
   component, through `fn_00426575`.
2. For each of the five entries `n` of the list at `0x00494838` that is not
   -1, naming roster slot `g` of `player`, it copies:
   - the 97-by-34 card `(0,560)` of `PX00129` to `(208, 159 + 36 * n)`;
   - the gang definition's 64-by-64 portrait, cell `(64 * (id % 10),
     64 * (id / 10))` of `PX03000` for the definition's `id` (offset `0x1E`),
     scaled to 32 by 32 at `(209, 160 + 36 * n)`;
   - `6 * force` pixels of the 3-row green strip `(354,0)` of `PX00129` at
     `(243, 163 + 36 * n)`, where `force` is record byte 3; no track is copied
     first, so the track is whatever the card art holds there;
   - for each of `weapon`, `armor` and `misc` (record bytes 4, 5, 6) that is not
     -1, the 20-by-20 icon at `(120, 20 * icon)` of surface 5, `icon` being the
     item's field at offset `0x1E`, to `(242, 170 + 36 * n)`,
     `(263, 170 + 36 * n)` and `(284, 170 + 36 * n)`.
   - When the gang definition's `tech_level` (definition offset `0x82`) is
     below `tech_needed`, it draws over the whole card area
     `(208, 159 + 36 * n)-(305, 193 + 36 * n)` with black through
     `fn_004266A6` in its pattern mode.

`fn_00447ADB(weapon, armor, misc, recipient)` draws the marks on the screen,
after every change and on paint:

- For each item row `r` (0 to 2) whose flag is set it copies the 54-by-54 cell
  `(414,13)` of `PX00129` with mode 1 to the screen at `(206, 138 + 64 * r)`;
  for a clear flag it copies the same area back from surface 7
  `(102, 158 + 64 * r)`.
- It restores the 32-by-180 column screen `(274,139)-(306,319)` from surface 7
  `(170,159)`, and when `recipient` is not -1 copies the 32-by-32 cell
  `(128,448)` of `PX00129` with mode 1 to the screen at `(274, 140 + 36 *
  recipient)`, left of that recipient's card.

## Interpretation

Each recipient card shows the gang's portrait at half size, its Force as the
same six-pixels-per-point meter the detailed sector screen uses (FND-UI-036),
and its three items as icons. A recipient whose gang type's Tech Level is
lower than the highest Tech Level among the selected items is covered by a
black pattern, and the handler refuses it (FND-GIVE-001). The item frames lie
one pixel outside the item cells local `(103, 15 + 64 * r)`, and the
recipient marker is the right-pointing arrow the Move panel uses for +1
(FND-MOVE-005).

## Alternatives

- Which colour the value 48,000 in each component produces, and which pattern
  `fn_004266A6` uses, have not been read.
- The list scan in the handler has no bound (FND-GIVE-001); this function
  reads only the five entries.

## How to reproduce

In `0x00448027`, find the loop of 5 over `0x00494838`, the rectangles with top
`0x9F + 36 * n` and left `0xD0`, the source `(0x230,0,0x252,0x61)`, the portrait
copy from surface 3 to a 32-by-32 destination, the multiply of the byte at
`0x00498DAB` by 6, the icon copies from x `0x78` of surface 5, and the
comparison of the word at `0x004A2882` with the second argument before the
call to `0x004266A6`. In `0x00447ADB`, find the source rectangles
`(0xD,0x19E,0x43,0x1D4)` and `(0x1C0,0x80,0x1E0,0xA0)` and the point
`(0x112, 0x8C + 0x24 * recipient)`.
