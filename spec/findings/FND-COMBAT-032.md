---
id: FND-COMBAT-032
title: The Detailed Combat clip player puts the strips' first frames on the screen only, and definition 63 changes only the attack strip
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00430CE0..0x00430DE8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00430FE3..0x004310B7
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004310BC..0x004310E8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042E6A3..0x0042E7C7
tool: Ghidra 12.1.3
environment: null
---

## Observation

- After setting up the bars, `fn_00430C23` builds the rectangle top `0xFE`,
  left `0xFE`, bottom `0x13E`, right `0x13E` and the rectangle top 0, left 0,
  bottom `0x40`, right `0x40`, and calls `fn_0042773E(7, 0, ...)` with them at
  `0x00430D5C`. It then builds top `0xFE`, left `0x147`, bottom `0x13E`, right
  `0x187` and top `0x40`, left 0, bottom `0x80`, right `0x40`, and makes the
  same call at `0x00430DE3`. These are the only copies before the loop that
  reads input with `fn_00462579` at `0x00430DF4`.
- The loop dispatches the event type at `0x004310BC..0x004310E8`: 2 to
  `0x00430E3F`, 3 to `0x00430EA5` and 7 to `0x00430FE3`. The type 7 branch
  calls `fn_0045CD70(7, ...)` at `0x00430FF3`, builds the rectangle top
  `0x7C`, left `0x68`, bottom `0x14D`, right `0x1C0` and the rectangle top
  `0x90`, left 0, bottom `0x161`, right `0x158`, calls `fn_0042773E(7, 0,
  ...)` at `0x00431092` and calls `fn_0045CDA4` at `0x004310AA`.
- The strip frame case of the tick dispatch (case 1 of FND-COMBAT-016, from
  `0x00431125`) copies frame `f` of each strip from surface 7, the rectangles
  top 0 and `0x40`, left `0x40 * f`, 64 by 64, into surface 7 at top `0x112`,
  left `0x96` and top `0x112`, left `0xDF`, with `fn_0042773E(7, 7, ...)`,
  and then copies those two rectangles of surface 7 to the screen rectangles
  of the setup.
- In `fn_0042E040`, for an unarmed attacker, the attack strip local
  `[ebp - 0x128]` gets 0 and the hit strip local `[ebp - 0x10]` gets 2 at
  `0x0042E6BD` and `0x0042E6C7`, and 1 and `0x12` at `0x0042E6F9` and
  `0x0042E703` when the definition's base Martial Arts word is above 0. At
  `0x0042E70A..0x0042E724` the definition number is compared with `0x3F`, and
  when equal only `[ebp - 0x128]` is set, to 2. The damage tests from
  `0x0042E786` follow (FND-COMBAT-010).

## Interpretation

Surface 7 holds the two strips in its top 128 rows and the panel's back buffer
in rows 144 to 353, which the paint branch copies to the panel's place on the
screen (FND-UI-001). At its setup the clip player copies frame 0 of
both strips straight to the screen apertures `(254, 254)` and `(327, 254)`,
and not into the back buffer, so from the clip's start until tick 3 the
screen shows frame 0 while the back buffer's apertures hold whatever was drawn
there before the clip. From tick 3 each frame goes into the back buffer first.
The type 7 event is `WM_PAINT` (FND-UI-020): the branch copies the whole panel
from the back buffer to the screen at `(104, 124)`, so a paint during ticks 0
to 2 replaces frame 0 in the apertures with the back buffer's content until
tick 3 draws the next frame. A paint at tick 3 or later changes nothing.

Definition 63 replaces only the attack strip: an unarmed attacker of that
definition plays attack strip 2 with the hit strip its Martial Arts picks.

## Alternatives

What the back buffer's apertures hold at the start of a later clip is not read
here; the presenter draws the panel before each clip. EXP-UI-049 shows them
black at tick 2 of a second clip, which this reading explains only if a paint
came before tick 3, and that run did not record paints.

## How to reproduce

In `fn_00430C23`, follow the two calls of `0x0042773E` before the first call
of `0x00462579`, and the comparisons of the event type with 2, 3 and 7 after
it; follow the branch for 7 to the calls of `0x0045CD70` and `0x0045CDA4`. In
`fn_0042E040`, find the read of the base Martial Arts word at `0x0042E6DF` and
the comparison with `0x3F` after it.
