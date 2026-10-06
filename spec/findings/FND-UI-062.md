---
id: FND-UI-062
title: The held-button helper draws the lit face of its kind while the pointer is inside and the plain face when it leaves and when the button comes up
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00418821..0x00418CCB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00418CCC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00425EDF
  - build: BLD-GOG-EN-1.1
    file: DATA/PX16/PX00129
    offset: 0x36..0xA1836
tool: Ghidra 12.1.3, and an image viewer for the sheet's faces
environment: null
---

## Observation

Function extents are those of FND-EXE-004. `fn_00425EDF(top, left, bottom,
right)` packs a rectangle; rectangles below are `(left,top)` with a width and
height on the sheet `PX00129` (surface 6).

`fn_00418821(kind, rect)` takes a face kind and the screen rectangle of the
control. For each kind it builds two rectangles of the sheet: one it copies
first, called the lit face below, and one it copies last, called the plain
face, and it plays one effect slot:

| Kind | Lit face | Plain face | Slot |
|---|---|---|---|
| 0 | `(0,386,50,23)` | `(50,386,50,23)` | 3 |
| 1 | `(0,409,50,23)` | `(50,409,50,23)` | 3 |
| 2 | `(120,281,32,13)` | `(120,268,32,13)` | 2 |
| 3 | `(120,205,32,63)` | `(460,211,32,63)` | 3 |
| 4 | `(97,560,50,23)` | `(147,560,50,23)` | 3 |
| 5 | `(197,560,50,23)` | `(247,560,50,23)` | 3 |
| 6 | `(337,560,50,23)` | `(387,560,50,23)` | 3 |

After the slot it copies the lit face with `fn_0042773E` straight to the window
(surface 0) at the control's rectangle and sets a flag. It then loops while the
left-button byte of the pointer record is set, reading the record again on each
pass (FND-UI-046). On a pass where the pointer is outside the rectangle
(`fn_00449B78`) and the flag is set, it copies the plain face and clears the
flag; on a pass where the pointer is inside and the flag is clear, it copies
the lit face and sets the flag. When the button comes up it copies the plain
face and returns the flag.

The key-press helper `fn_00418CCC(kind, rect)` builds the same pair for kinds
0, 1 and 3, plays slot 3, copies the lit face, waits one tick with
`fn_00464CD9(1)` and copies the plain face (FND-UI-019).

The sheet's lit faces of kinds 0 and 1 have a bright green frame; the plain
faces have the panels' darker frame, and `(100,386,50,23)` is a dim face.

Callers by kind:

- Kind 0: the confirm faces of the Attack (`0x0043BE35`), Equip (`0x0043E2B7`),
  Influence (`0x00440AE4`), Move (`0x00441FE9`), Research (`0x00442DB0`), Sell
  (`0x00444CC4`) and Give (`0x00446E42`) panels; Comlink Send (`0x0045F66F`);
  the exit or close faces of Last Turn Events (`0x0044FA24`), the gang
  information panel (`0x0044AED6`, `0x0044B016`), Item Information
  (`0x0044C04F`), `fn_0044C476` (`0x0044CE97`), `fn_0044D1BB` (`0x0044E4A2`),
  `fn_0044E6ED` (`0x0044F0A8`), `fn_00451F80` (`0x00452584`), `fn_004518D9`
  (`0x00451D3B`), `fn_004546C5` (`0x00454F3A`), `fn_0045519D` (`0x004558FF`),
  `fn_00455B6B` (`0x00456B34`, `0x00456C79`), `fn_0045D61A` (`0x0045DD20`) and
  `fn_00448E32` (`0x004490A2`); and the idle-gang warning's OK (`0x00448A3F`).
- Kind 1: the Cancel faces of the same seven command panels (`0x0043BD69`,
  `0x0043E1F5`, `0x00440A22`, `0x00441F27`, `0x00442CEE`, `0x00444BE5`,
  `0x00446D7D`) and of Comlink Send (`0x0045F5AA`); the idle-gang warning's
  Cancel (`0x0044899F`); and the exit of Detailed Combat (`0x00430FA1`).
- Kind 2: two calls in `fn_00416C75` (`0x00416E87`, `0x0041713A`).
- Kind 3: the sector view's back control (`0x0047108B`).
- Kinds 4 and 5: one call each in `fn_00448E32` (`0x0044913C`, `0x004492C5`).
- Kind 6: no caller.

The Search handler `fn_00448E32` tests the byte the helper returns after its
calls for ALL (`0x00449148`), NONE (`0x004492D1`) and Done (`0x004490AE`), and
Comlink Send after Send (`0x0045F67B`) and Cancel (`0x0045F5B6`); each skips
its action when the byte is clear.

## Interpretation

A held face shows the lit image while the pointer is over it and the plain
image while it is off it, and the plain image stays on the screen after the
button comes up, wherever it was released, until the panel draws over it. For
the confirm and Send faces the plain image is the available face
`(50,386,50,23)` that `fn_00418E66` draws. The exit of Detailed Combat is a
kind 1 face, so it shows the Cancel images. ALL, NONE and Done of the Search
panel, and Send and Cancel of Comlink Send, act only when the button comes up
inside them.

## Alternatives

- What the kind 2 controls are on screen was not read. Kinds 4 and 5 are
  taken as ALL and NONE because the Search handler tests the helper's result
  for ALL and NONE right after those two calls.

## How to reproduce

In `0x00418821`, read the switch on the first argument: each case calls
`0x00425EDF` twice, keeping the first rectangle for the copies after the loop
and when the pointer leaves, and the second for the first copy and when the
pointer comes back. List the calls of `0x00418821` and the constant each pushes
last, and read the test of `AL` after the calls named above.
