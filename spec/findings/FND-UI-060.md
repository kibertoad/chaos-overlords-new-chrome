---
id: FND-UI-060
title: The planning entry draws the console's year, week, countdown, score and cash with the base-value number helper
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046FF41
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046FF88
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046FFE7
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047007D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004700C0
tool: Ghidra 12.1.3
environment: null
---

## Observation

The five numeric draws of FND-UI-040 in the planning entry `fn_0046FD80` are
direct calls to `fn_00414187`. Each pushes five arguments and the caller
removes 20 bytes after the call. Read in push order from the last argument,
the leading-zero flag comes first, then the cell count, then the value, then
the point from `fn_00425F8C`, then the surface 1:

| Call | Point | Value | Cells | Leading-zero flag |
|---|---|---|---|---|
| `0x0046FF41` | `(481,15)` | `g_0049CA68 / 52 + 2050` | 4 | 0 |
| `0x0046FF88` | `(511,15)` | `g_0049CA68 % 52 + 1` | 2 | 1 |
| `0x0046FFE7` | `(562,15)` | `g_004A5EF8 - g_0049CA68 - 1` | 3 | 0 |
| `0x0047007D` | `(550,24)` | `g_004A2790[g_004ABC84]` | 5 | 0 |
| `0x004700C0` | `(550,42)` | `g_004A25E8[g_004ABC84]` | 5 | 0 |

Ghidra's list of references to `fn_004142E7`, the helper that draws 0 as the
dim cell (FND-UI-006), has no call site inside `fn_0046FD80`; the same kind of
list for `fn_00414187` holds the five calls above, which serves as the check
that the listing covers this function.

## Interpretation

The console's score and cash are base values in the sense of FND-UI-006: a
zero score or zero cash is the bright green 0 in the last of the five cells,
and a negative one is a red magnitude without a minus sign. The year and the
countdown are drawn the same way. The week is the one console number drawn
with the leading-zero flag set, so week 1 to week 9 show a bright 0 in the
first of their two cells, which is what FND-UI-040 calls leading zeroes.

## Alternatives

None. The callee and the pushed constants are read from the call
instructions, not from a decompiler's guess at the arguments.

## How to reproduce

Verify the executable against BLD-GOG-EN-1.1. In Ghidra, list the
instructions of `fn_0046FD80` from `0x0046FF20` to `0x004700C8`. Each of the
five `CALL 0x00414187` instructions is preceded by `PUSH` of the flag and the
cell count, the computation of the value, the point returned by the
`fn_00425F8C` call before it, and `PUSH 0x1`, and is followed by
`ADD ESP,0x14`. Then list the references to `0x004142E7` and to
`0x00414187` and look for call sites between `0x0046FD80` and the end of the
function.
