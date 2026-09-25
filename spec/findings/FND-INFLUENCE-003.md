---
id: FND-INFLUENCE-003
title: The Influence picker sits at screen (104,124), confirms with a control or Enter only when a site is chosen, cancels with a control or Escape, and preselects a pending Influence order
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043F6E6..0x0043F71B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00440716..0x00440798
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004407A2..0x0044124E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00414865..0x00414879
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00414AA7..0x00414ABB
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_0043F692(player, roster_slot)` (range in FND-EXE-004). Rectangles are
screen coordinates written `(x1,y1)-(x2,y2)`, half-open; "local" means screen
minus `(104,124)`, the subtraction the handler makes at `0x00440970` and
`0x00440DF3`.

- It loads resource 5005 into surface 7 at `(top=144, left=0, bottom=353,
  right=344)` (`0x0043F71B`) and slides it in with `fn_0041953E(0)`
  (`0x00440716`), the primary form of FND-UI-011, which ends at
  `(104,124)-(448,333)`.
- It copies the gang's 64-by-64 portrait to local `(26,17)`, from surface 3 by
  the definition's `+0x1E` field (`0x0043F727` onward).
- When the gang's action byte is already 9, it takes the gang's `target` byte
  as the selection, draws it with `fn_0044127B` and draws the confirmation
  face enabled with `fn_00418E66` (`0x00440726..0x00440798`). Otherwise
  nothing is selected.
- Key down (type 2): the value `0x2B` or `0x0D` plays sound slot 4 when nothing
  is selected (`0x0044089B`); otherwise it runs the press helper
  `fn_00418CCC(0, ...)` on the face `(137,293)-(187,316)`, writes the selection
  into the gang's byte 8 (`0x00440884`) and closes returning 1. The value
  `0x1B` runs `fn_00418CCC(1, ...)` on `(137,261)-(187,284)` and closes
  returning 0.
- Left button down (type 3), `0x00440918..`: outside `(104,124)-(448,333)`
  it plays slot 4. Inside, local `(33,137)-(82,159)` runs the pointer helper
  `fn_00418821(1, ...)` on the Cancel face `(137,261)-(187,284)` and closes
  returning 0 when it returns nonzero. Local `(33,169)-(82,191)` plays slot 4
  when nothing is selected, and otherwise runs `fn_00418821(0, ...)` on the
  confirmation face and, when it returns nonzero, writes the target
  (`0x00440B0C`) and closes returning 1. The three site rectangles of
  FND-INFLUENCE-001 then select a slot that is not complete: it redraws the
  site area from surface 7, draws the new selection with `fn_0044127B` and
  enables the confirmation face.
- Left double-click (type 5), `0x00440D9B..`: outside the panel it plays slot
  4. Inside, on an enabled site rectangle it opens the Site Information handler
  `fn_0044C476` with the site's definition, 1, and whether the sector's owner
  is the active player (`0x00440F61`), then redraws the current selection. It
  does not change the selection. No other region is tested on a double-click.
- Paint (type 7) restores the screen and panel and redraws the selection and
  the face.
- On exit it slides the panel out with `fn_004196F5(0, 0)`, calls
  `fn_004120CB` and stores 1 at `0x00498100` (`0x0044125A..0x00441267`).
- Both callers, the command handlers `fn_0041462F` and `fn_00414D8C`, store
  action 9 for the gang when the handler returns nonzero (`0x00414879`,
  `0x00414ABB`); a return of 0 leaves the action as it was.

## Interpretation

On the screen the site slots are `(210,141)-(330,205)`, `(312,197)-(432,261)`
and `(210,251)-(330,315)`, the places where FND-INFLUENCE-002 draws the site
pictures, so what the player clicks is the picture. Cancel is the 49-by-22
face at `(137,261)` and confirmation the one at `(137,293)`, the same places
as on the Move panel (FND-MOVE-004). Enter and Execute confirm, Escape
cancels. Confirmation writes the site slot into `target`, and the command box
that opened the picker writes the action; reopening the picker for a gang
already ordered to Influence shows its choice. The interpretation of
FND-INFLUENCE-001 that the input rectangles differ from the drawn pictures is
wrong: they coincide.

## Alternatives

None known.

## How to reproduce

In `fn_0043F692`, read the event switch after the call to `fn_00462579` at
`0x004407A2`: the key comparisons with `0x2B`, `0x0D` and `0x1B`, the panel
test `fn_00425EDF(0x7C, 0x68, 0x14D, 0x1C0)`, and the local rectangles
`(0x89, 0x21, 0x9F, 0x52)` and `(0xA9, 0x21, 0xBF, 0x52)` in `(top, left,
bottom, right)` order.
