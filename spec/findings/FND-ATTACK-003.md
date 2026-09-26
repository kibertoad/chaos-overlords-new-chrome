---
id: FND-ATTACK-003
title: The Attack picker sits at (104,124), lists the other five players in slot order, enables an opponent by the sector's gangs_seen byte, and confirms with Enter, plus or its lower face and cancels with Escape or its upper face
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043B290..0x0043D072
tool: Ghidra 12.1.3
environment: null
---

## Observation

In `fn_0043B290(player, slot)` (range in FND-EXE-004). Rectangles are
`(left, top)-(right, bottom)`; local coordinates are relative to the panel's
top-left corner.

- It copies the acting gang's record (`0x0043B2B5`), loads resource `0x138B`
  (5003) into surface 7 (`0x0043B320`) and copies it into buffer rows 144 to
  353. Its pointer tests take screen `(104,124)-(448,333)` as the panel and
  subtract 104 and 124 from the pointer (`0x0043BC71`, `0x0043BCB7`), so the
  panel's origin on screen is `(104,124)`.
- The acting gang's portrait is the 64 by 64 cell of surface 3 chosen by the
  definition's word at `0x004A281E + definition * 0x9C` (`0x0043B3DA`), drawn at
  local `(26,17)-(90,81)`. Each equipped item (weapon, armor, miscellaneous)
  gets a 20 by 20 icon from surface 5, column taken from the item record's
  `id` word (`0x004A5F26 + item * 0xA6`), at local `(26,82)`, `(48,82)` and
  `(70,82)` (`0x0043B4A7..0x0043B6D5`). A definition byte of -1 draws a blank
  from surface 6 instead (`0x0043B3D1`).
- The opponent cells (`0x0043B795..0x0043B9C1`) are the other five player
  slots in ascending order, skipping the acting player, in cells
  `k` 0 to 4 at local `(98, 16 + 36k)`. A cell is enabled when the byte at
  offset `0x10 + opponent` of the acting gang's sector record (`gangs_seen`,
  FMT-STATE-002) is nonzero (`0x0043B7C4`); the enabled and disabled cells take
  their art from surface 6 rows 480 and 594, column by the opponent's colour
  byte `0x004A5F00 + opponent`.
- The first enabled cell is chosen when the picker opens; when the gang's
  action byte is already 1 and its `target` byte names an enabled opponent,
  that opponent is chosen instead (`0x0043B8BE..0x0043B8EB`). The chosen
  opponent's gangs are listed by `fn_0043D132(opponent, sector)`
  (`0x0043B9D8`) into the six-entry list at `0x00494850`. When the action is
  already Attack, the cell whose list entry equals the `target_2` byte becomes
  the chosen target (`0x0043BA08..0x0043BA3B`).
- A click in local `(135,16)-(337,193)` selects cell `3 * (y > 104) +
  (x > 201) + (x > 269)` when the list entry for that cell is not -1
  (`0x0043C298..0x0043C319`).
- Keys (event 2): `0x2B` or `0x0D` confirms when both an opponent and a target
  are chosen, and otherwise calls `fn_00464290(4)`
  (`0x0043BB10..0x0043BBE2`); `0x1B` draws the pressed look over screen
  `(137,261)-(187,284)` and closes without an order (`0x0043BBEA..0x0043BC4C`).
  No other key is tested.
- Faces: a click in local `(33,137)-(82,159)` tracks the screen face
  `(137,261)-(187,284)` and closes without an order when released on it
  (`0x0043BCD7..0x0043BD7D`); a click in local `(33,169)-(82,191)` confirms
  under the same condition as the keys, tracking screen `(137,293)-(187,316)`
  (`0x0043BD8F..0x0043BE9B`). A click outside the panel calls
  `fn_00464290(4)` (`0x0043C4A5`).
- The confirm writes the chosen opponent's player slot into byte `0x08` of the
  gang record and the list entry of the chosen cell into byte `0x09`
  (`0x0043BB9B`, `0x0043BBBB`, `0x0043BE54`, `0x0043BE74`). The action byte is
  not written here.

## Interpretation

The picker is at the same origin as the other command panels. Its opponents
are the other players in slot order, and one can be picked only when the
acting player sees at least one of its gangs in the sector. The upper face
under the portrait is Cancel and the lower face is Confirm; Escape is Cancel,
Enter and plus are Confirm. Reopening the picker for a gang that already
attacks shows the current order selected.

## Alternatives

- Which resources surfaces 3 and 5 hold while the picker is open was not
  traced.
- The handler's event 5 branch (`0x0043C4BC`), which tests the portrait and
  equipment rectangles, was not read.

## How to reproduce

In `fn_0043B290`, find the load of `0x138B`, the opponent loop that reads
`0x004A08F8 + opponent + sector * 0x24`, the event switch with the constants
`0x2B`, `0x0D` and `0x1B`, the face rectangles `(0x89,0x21,0x9F,0x52)` and
`(0xA9,0x21,0xBF,0x52)`, and the stores into `0x00498DB0` and `0x00498DB1`.
