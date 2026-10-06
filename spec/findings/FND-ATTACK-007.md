---
id: FND-ATTACK-007
title: The Attack picker copies a target's second and third item icons into 19-pixel-wide boxes, so they are stretched
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043D613..0x0043D649
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043D6F7..0x0043D72D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043D7DB..0x0043D811
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the list builder `fn_0043D132` (FND-ATTACK-005), each of the three item
icons of a target card is copied with `fn_0042773E` from a source rectangle
that `fn_00425F4D` builds with a width and height of `0x14`, 20 by 20, to a
destination rectangle that `fn_00425EDF(top, left, bottom, right)` builds:

| Item | Source size | Destination `(top, left, bottom, right)` | Destination size |
|---|---|---|---|
| `weapon` | `0x0043D613` | `(0x42, 1, 0x56, 0x15)`, `0x0043D641..0x0043D649` | 20 by 20 |
| `armor` | `0x0043D6F7` | `(0x42, 0x17, 0x56, 0x2A)`, `0x0043D725..0x0043D72D` | 19 by 20 |
| `misc` | `0x0043D7DB` | `(0x42, 0x2D, 0x56, 0x40)`, `0x0043D809..0x0043D811` | 19 by 20 |

## Interpretation

The weapon's icon is copied at its own size. The armor and misc icons go into
boxes one pixel narrower than the icon, so `fn_0042773E` takes its
`StretchBlt` path for them (FND-GFX-004), and the boxes end at x 42 and 64 of
the card instead of 43 and 65. Which source column the stretch leaves out is
not given by the code.

## Alternatives

None known.

## How to reproduce

In `0x0043D132`, after the Force track, find the three calls of `0x00425F4D`
with `0x14` and `0x14` and the call of `0x00425EDF` after each, with the
pushes `0x15`, `0x56`, `1`, `0x42`; `0x2A`, `0x56`, `0x17`, `0x42`; and
`0x40`, `0x56`, `0x2D`, `0x42`.
