---
id: FND-GIVE-001
title: The Give panel handler lists the giver's sector mates, accepts a recipient only when its Tech Level covers every selected item, and stores the order in the target bytes
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00445A4F..0x00447ADA
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00494838..0x0049484B
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_00445A4F` occupies `0x00445A4F..0x00447ADA` (8,290 bytes, FND-EXE-004). Its
only caller is the gang command handler `fn_00414D8C` at `0x00415794`, which
calls it when the chosen action is 6 and cancels the action when it returns 0.
It takes a player slot and a roster slot and returns 1 when it stored an order.

Set-up, in order:

- It copies the acting gang's 32-byte record (FMT-STATE-001) to the stack,
  saves the screen with `fn_004120A7`, and loads resource 5015 (`PX05015`)
  into surface 7 at `(top=144, left=0, bottom=353, right=344)`.
- It copies the giver's 64-by-64 portrait, the cell `(64 * (id % 10), 64 *
  (id / 10))` of surface 3 for the gang definition's `id` (definition offset
  `0x1E`), to surface 7 `(26,161)`.
- For each of `weapon`, `armor` and `misc` that is not -1 it loads the item's
  720-by-48 strip, resource `4000 + id` (item offset `0x1E`), into rows 0, 48
  and 96 of surface 7, and copies frame 0 to surface 7 x 105, y 161, 225 and
  289.
- It fills the five-entry list at `0x00494838` with -1, then scans the
  player's 81 roster records and appends every roster slot, other than the
  giver's, whose `sector` byte equals the giver's. The scan does not compare the
  count with five.
- When the giver's `action` is already 6, it restores the selection from the
  record: bit 0, 1 and 2 of `target` select the weapon, armor and
  miscellaneous item, and the recipient is the list entry equal to `target_2`.
  It then computes the highest `tech_level` (item offset `0x80`) of the
  selected items. Otherwise nothing is selected, no recipient is chosen and the
  highest Tech Level is 0.
- It draws the recipient list with `fn_00448027(player, highest Tech Level)`,
  slides the panel in with `fn_0041953E(0)`, draws the confirm face
  `(top=293, left=137, bottom=316, right=187)` enabled only when the giver's
  action was already 6 (`fn_00418E66`), and draws the selection marks with
  `fn_00447ADB(weapon, armor, misc, recipient)`.

Its loop reads one event at a time through `fn_00462579` and handles:

- Key down: Enter (`0x0D`) or Execute (`0x2B`) with the confirm face enabled
  presses it through `fn_00418CCC(0, ...)`, writes the order and ends; with the
  face disabled it plays slot 4. Escape presses the Cancel face
  `(top=261, left=137, bottom=284, right=187)` through `fn_00418CCC(1, ...)`
  and ends without an order.
- Left button down: outside the panel rectangle `(104,124)-(448,333)` it plays
  slot 4. Inside, it works in panel-local coordinates (x - 104, y - 124). The
  Cancel face, local `(33,137)-(82,159)`, ends without an order when
  `fn_00418821(1, ...)` reports a release inside. The confirm face, local
  `(33,169)-(82,191)`, plays slot 4 while it is disabled; enabled, it writes
  the order and ends when `fn_00418821(0, ...)` succeeds. The item cells, local
  `(103,15)-(155,67)`, `(103,79)-(155,131)` and `(103,143)-(155,195)`, toggle
  a slot that holds an item. The recipient rows, local
  `(208, 15 + 36n)-(305, 49 + 36n)` for `n` 0 to 4, choose entry `n` when it is
  not -1 and its gang definition's `tech_level` (definition offset `0x82`) is
  at least the highest Tech Level of the selected items.
- After any of those changes it recomputes the highest Tech Level. When the
  value changed it redraws the recipient list, drops the chosen recipient if
  its Tech Level is now too low, and copies the list area to the screen. It then
  redraws the marks and enables the confirm face only when at least one item
  and a recipient are selected.
- Left double-click, local coordinates: on an item cell with an item, the Item
  Information panel `fn_0044B699` for that item; on the giver's portrait,
  local `(26,17)-(90,81)`, the Gang Definition Information panel
  `fn_00455B6B`; on a recipient's portrait, local
  `(209, 16 + 36n)-(241, 48 + 36n)`, the same panel for that gang; on one of a
  recipient's three 20-by-20 item icons, local x `242..261`, `263..282` and
  `284..303`, y `26 + 36n..45 + 36n`, Item Information for that item. Each
  redraws the confirm face and the marks on return.
- Paint: restores the screen from surface 1 and the panel from surface 7, and
  redraws the confirm face and the marks.
- When no event arrives and the animation timer `fn_004328BE(0)` has expired,
  it advances a frame counter through 0 to 14 and copies that 48-by-48 frame of
  each carried item's strip to screen x 209, y 141, 205 and 269.

Writing the order sets the giver's `target` (record offset 8) to
`weapon + 2 * armor + 4 * misc` and `target_2` (offset 9) to the chosen
recipient's roster slot. The handler never writes `action`. On exit it slides
the panel out with `fn_004196F5(0, 0)`, restores the screen with
`fn_004120CB`, and sets the byte at `0x00498100` to 1.

## Interpretation

Give moves any subset of the three carried items to one gang of the same
player in the same sector. A recipient qualifies only when its gang type's
Tech Level is at least the highest Tech Level among the items selected, and
selecting a higher-level item can drop a recipient already chosen. The
recipient's Tech Level is that of its gang type; the items it carries do not
change it. The order lives in `target` as a three-bit item mask and in
`target_2` as the recipient's roster slot; the command box writes the action.

## Alternatives

- The recipient list is drawn by `fn_00448027`, which has not been read; how it
  marks recipients that fail the Tech Level test is not recorded here.
- Whether a player can have more than six gangs in one sector, which would make
  the unbounded scan write past the five list entries into the next global, has
  not been checked.

## How to reproduce

In `0x00445A4F`, find the load of resource `0x1397`, the loop over `0x51`
records comparing the sector byte at `0x00498DAA`, the test of `action`
against 6, the three rectangle tests with left 103 and the loop with top
`15 + 36n` and right `0x131`, the comparison with the definition word at
`0x004A2882`, and the two writes to `0x00498DB0` and `0x00498DB1`.
