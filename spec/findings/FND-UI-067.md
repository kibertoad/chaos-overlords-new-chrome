---
id: FND-UI-067
title: Every information panel holds its close face through the held-button helper, closes only on a release inside it and refuses a press outside its test rectangle
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00449E80
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044B699
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044C476
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044D1BB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044E6ED
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004518D9
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00451F80
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004546C5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045519D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00455B6B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045D61A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00448718
  - build: BLD-GOG-EN-1.1
    file: DATA/PX16/PX00129
    offset: 0x36..0xA1836
tool: Ghidra 12.1.3, and an image viewer for the panels' faces
environment: null
---

## Observation

Function extents are those of FND-EXE-004; event types are those of FND-UI-020
(3 a left press, 5 a left double-click). `fn_00425EDF(top, left, bottom,
right)` packs a rectangle; `fn_00449B78` tests a point against one, and
`fn_00418821` is the held-button helper of FND-UI-062. Rectangles below are
written `(left,top)-(right,bottom)` in screen coordinates.

Each handler below takes event types 3 and 5 in one case (the gang information
panel in two cases with the same tests). The case tests the pointer against an
outside rectangle and plays effect slot 4 through `fn_00464290(4)` when it lies
outside; otherwise it moves the point to panel-local coordinates, tests a
half-open face rectangle, and calls `fn_00418821` with a face kind and the
helper rectangle. After every call it tests the byte the helper returns
(`MOV CL,AL`, `TEST ECX,ECX`) and sets the loop's exit flag only when it is
set.

| Handler | Panel | Outside test | Face test, panel-local from | Helper call, kind, rectangle |
|---|---|---|---|---|
| `fn_00449E80` | Gang information | `(104,124)-(448,333)` | `(33,169)-(82,191)` from `(104,124)` | `0x0044AED6` and `0x0044B016`, 0, `(137,293)-(187,316)` |
| `fn_0044B699` | Item Information | `(128,124)-(448,333)` | `(33,169)-(82,191)` from `(128,124)` | `0x0044C04F`, 0, `(161,293)-(211,316)` |
| `fn_0044C476` | Site Information | `(128,124)-(448,333)` | `(33,169)-(82,191)` from `(128,124)` | `0x0044CE97`, 0, `(161,293)-(211,316)` |
| `fn_0044D1BB` | Financial | `(104,124)-(448,333)` | `(33,169)-(82,191)` from `(128,124)` | `0x0044E4A2`, 0, `(161,293)-(211,316)` |
| `fn_0044E6ED` | Gangs in Sector | `(104,124)-(448,333)` | `(33,169)-(82,191)` from `(104,124)` | `0x0044F0A8`, 0, `(137,293)-(187,316)` |
| `fn_004518D9` | Player Rankings | `(104,124)-(448,333)` | `(33,169)-(82,191)` from `(104,124)` | `0x00451D3B`, 0, `(137,293)-(187,316)` |
| `fn_00451F80` | Combat Results | `(104,124)-(448,333)` | `(33,169)-(82,191)` from `(104,124)` | `0x00452584`, 0, `(137,293)-(187,316)` |
| `fn_004546C5` | Hire comparison | `(104,124)-(448,333)` | `(57,169)-(106,191)` from `(104,124)` | `0x00454F3A`, 0, `(161,293)-(211,316)` |
| `fn_0045519D` | Game Information | `(104,124)-(448,333)` | `(33,169)-(82,191)` from `(128,124)` | `0x004558FF`, 0, `(161,293)-(211,316)` |
| `fn_00455B6B` | Gang definition information | `(128,124)-(448,333)` | `(33,169)-(82,191)` from `(128,124)` | `0x00456B34` and `0x00456C79`, 0, `(161,293)-(211,316)` |
| `fn_0045D61A` | Comlink View | `(104,124)-(448,333)` | `(33,169)-(82,191)` from `(104,124)` | `0x0045DD20`, 0, `(137,293)-(187,316)` |
| `fn_00448718` | Idle-gang warning, Cancel | `(104,124)-(448,333)` | `(33,137)-(82,159)` from `(104,124)` | `0x0044899F`, 1, `(137,261)-(187,284)` |
| `fn_00448718` | Idle-gang warning, OK | `(104,124)-(448,333)` | `(33,169)-(82,191)` from `(104,124)` | `0x00448A3F`, 0, `(137,293)-(187,316)` |

In Comlink View the Previous and Next tests come between the outside test and
the face test.

The face each panel's art holds at the helper rectangle's corner, compared with
the plain kind 0 face `(50,386,50,23)` of `PX00129`: the art of Gang
information, Site Information, Gangs in Sector, Player Rankings, Combat
Results, Hire comparison, Game Information, Gang definition information,
Comlink View and the idle-gang warning's OK is the same image, and the
warning's Cancel is the plain kind 1 face `(50,409,50,23)`. The art of Item
Information and of both Financial images draws the same face with its frame
one shade brighter.

## Interpretation

Every panel of the table closes only when the left button comes up inside its
close face, as Search and Comlink Send do (FND-UI-062), and shows the lit face
while the face is held under the pointer. A press outside the outside rectangle
is refused with slot 4. The Financial and Game Information panels test the
outside of the full 344-pixel panel rectangle although they are drawn 320
pixels wide from x 128.

The helper's last copy is the plain face, so a release outside the face leaves
the plain face on the panel. On Item Information and Financial that face
differs from the panel's art by one shade of its frame.

## Alternatives

- Whether a later copy of Item Information or Financial covers the plain face
  before the panel closes was not read.

## How to reproduce

In each handler of the table, find the case for event types 3 and 5 of the
dispatch switch, read the `fn_00425EDF` constants before the two
`fn_00449B78` tests and before the call of `0x00418821`, and the instructions
after the call. Compare the panel images' rectangle at the face's panel-local
corner with the sheet's faces.
