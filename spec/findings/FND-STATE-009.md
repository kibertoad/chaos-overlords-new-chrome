---
id: FND-STATE-009
title: Game code reads nine .rdata constants, all but one in the computer players' planning pass; two initialized .data tables of sines and cosines are used only by uncalled helpers
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00481000..0x00481047
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00482180..0x00484E7F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045B777
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045CF21..0x0045CF43
tool: Ghidra 12.1.3
environment: null
---

## Observation

This is the third part of the data map of FND-STATE-007.

`.rdata`. 100 distinct `.rdata` addresses are referenced. Nine are read by game
code, 42 only by functions of the statically linked C runtime (from
`0x004787E0` on, FND-EXE-004: the program entry, the floating-point output
routines, the locale and string-type helpers and the runtime's message box),
and 49 only from data: the executable's header and pointer tables in `.data`
that the runtime owns. The nine that game code reads form one table at the
start of the section:

| Address | Type | Value | Read by |
|---|---|---|---|
| `0x00481010` | double | 1.5 | `fn_00458FA0` at `0x004595F9`, the hire limit of scenario 0 (FND-AI-012) |
| `0x00481018` | float | 52 | `fn_00458FA0`, the match-length divisor (FND-AI-009, FND-AI-050) |
| `0x0048101C` | float | 4 | `fn_00458FA0`, ten quota tests (FND-AI-050) |
| `0x00481020` | float | 2 | `fn_00458FA0`, five quota tests (FND-AI-050) |
| `0x00481024` | float | 100 | `fn_00458FA0`, two quota tests (FND-AI-050) |
| `0x00481028` | float | 3 | `fn_00458FA0`, nine quota tests (FND-AI-050) |
| `0x0048102C` | float | 6 | `fn_00458FA0` at `0x0045A2DF` (FND-AI-050) |
| `0x00481030` | float | 10 | `fn_00458FA0` at `0x0045B777` only |
| `0x00481040` | double | 2.0 | `fn_0045CF05` at `0x0045CF21` and `0x0045CF43` only |

`0x00481030` is read once, in the hire-role case 7 of the planning pass
(`0x0045B777`): the pass multiplies its match-length ratio by 10 and compares
the product with the value of a selector call, and clears a counter when the
product is not greater (FND-AI-050 gives the quota tests of this form).

`0x00481040` is read twice by `fn_0045CF05`, which multiplies an angle in
degrees, already reduced to 0 to 359 by `fn_0045CE61`, by 2.0 to index the two
tables below. `fn_0045CF05` has no callers (FND-TIMER-002).

The double 1.0 at `0x00481000` and the float 140 at `0x00481008` are not read by
any instruction; the first is referenced only from the executable's header.

Initialized `.data`. Of the 427 initialized `.data` addresses game code
references, 254 are the starts of strings (file names, registry and
window-class names, format strings and texts). The others are variables with
values in the image. Two of them are numeric tables:

- `0x00482180..0x004837FF`: 720 doubles, the cosine of 0, 0.5, 1, ... 359.5
  degrees; the first entry is 1.0.
- `0x00483800..0x00484E7F`: 720 doubles, the sine of the same angles; the first
  entry is 0.

Only `fn_0045CF05` (four references) and `fn_0045D17E` (four references) read
them. `fn_0045D17E` is called only by `fn_0045CF99`, which has no callers
(FND-TIMER-002).

## Interpretation

Apart from the 2.0 of the geometry helpers, every constant game code loads
from `.rdata` is read by the computer players' planning pass; no other game
function reads `.rdata`. The sine and cosine tables and the 2.0 belong to a geometry
helper set that no code path reaches, so they have no effect on play. The misdirected
store of FND-AI-043 writes into the cosine table at `0x00482198`, where nothing
that runs reads it.

## Alternatives

A constant loaded through a computed address would not show as a reference;
none of the nine is next to data that looks like a table indexed at run time
other than the float run `0x00481018..0x00481030`, whose every entry has a
direct reference.

## How to reproduce

List the references to `.rdata` and sort them by the function that holds the
instruction; separate the functions below and above `0x004787E0`. For the
tables, list the references to `0x00482180..0x00484E7F` and the callers of
`fn_0045CF05`, `fn_0045CF99` and `fn_0045D17E`.
