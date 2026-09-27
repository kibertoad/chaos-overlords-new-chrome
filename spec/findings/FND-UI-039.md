---
id: FND-UI-039
title: The end-of-match planning visit differs from an ordinary one only through the Done light flag and the no-match byte
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048780C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046F896..0x0046F924
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004631A4..0x00463259
tool: Python 3.14.7 script disassembling the file bytes with Capstone 5.0.7
environment: null
---

## Observation

Function extents are those of FND-EXE-004. The byte at `0x0048780C` has four
references in the executable, all byte moves:

| Address | Function | Access |
|---|---|---|
| `0x0046E8B4` | match entry `fn_0046E766` | stores 0 at match start |
| `0x0046F8FB` | match entry `fn_0046E766` | stores 1 just before the call of the planning visit `fn_0046FD80` at `0x0046F902` |
| `0x0046F907` | match entry `fn_0046E766` | stores 0 just after that call returns |
| `0x004631A4` | event pump `fn_00462579` | reads it; when it is set, the lit step copies `PX00129` (surface 6) `(488,512)-(496,528)` to the window at `(592,282)-(600,298)` |

The end block `0x0046F896..0x0046F924` visits the slots 0 to 5 in order and
acts only on a slot whose type dword at `0x004AB638` is 0. For such a slot it
calls the handoff card `0x004396C0` when `0x004ABC98` is set, stores the slot
in `0x004ABC84`, and then tests the slot's active byte at `0x004ABBE0`: when it
is set, it runs the planning visit with `0x0048780C` set; when it is 0, it
calls the elimination card `0x0042C3F5` with the slot. After the last slot it
calls the awards controller `0x0042B9E0`.

The planning visit clears `0x004ABC60` at `0x0046FDAA`, on entry.

## Interpretation

The only reader of `0x0048780C` is the lamp step of the event pump, so the
flag's one effect is the blinking Done light (FND-EVENT-006). The other
differences of the final visit from an ordinary planning visit are those of
the no-match byte `0x004ABC9C` (FND-STATE-010): no planning clock, no
idle-gang warning and no offer to save. The byte `0x004ABC60` that the end
block sets before the visits is cleared again on entry to each visit, so it
makes no difference inside one. `0x004ABC84` is the viewed player the planning
visit draws and acts for, so each visit shows the city as that slot sees it.

## Alternatives

None known.

## How to reproduce

List the references to `0x0048780C` in the disassembly of
`Chaos Overlords.exe`, then read `fn_0046E766` from `0x0046F896` to the call
at `0x0046F924` and the event pump from `0x004631A4` to `0x00463259`.
