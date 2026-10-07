---
id: FND-AI-079
title: Only the family handlers and their dispatcher store a planned action, and none stores Bribe, Give or Sell
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048A258..0x0048A25B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00432DA0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00458FA0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00409DE1
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ghidra resolves 200 instruction references to the planned action byte of the
planning records (`0x0048A258 + player * 0x510 + slot * 0x10`, offset `+0x08`
of FMT-STATE-007). 198 of them are byte stores and 2 are loads. Every store
writes an immediate value; none stores a register. They lie in these
functions:

| Function | Role | Stores | Values stored |
|---|---|---|---|
| The 14 family handlers of FND-AI-001 | planning | 191 | 0, 1, 3, 4, 5, 7, 8, 9, 10, 11, 13, 14 |
| `0x00432DA0` | the family dispatcher (FND-AI-001) | 4 | 1, 8, 10 |
| `0x00458FA0` | the planning pass (FND-AI-019) | 2 | 0 |
| `0x00409DE1` | the record reset (FND-AI-019) | 1 | 0 |

The handler column counts the stores of `0x00428EF0`, `0x00434080`,
`0x0041FEF0`, `0x00435BD0`, `0x00401000`, `0x0043A1D0`, `0x00431C60`,
`0x00436C70`, `0x004605E0`, `0x0042A6E0`, `0x00420950`, `0x004353A0`,
`0x0040ABC0` and `0x00466910`. No store anywhere writes 2 (Bribe), 6 (Give)
or 12 (Sell). The two loads are in the query function `0x00402D70` (selector
`0x3D`) and in `0x00458FA0`'s history copy. The save and load functions
`0x00463CC5` and `0x0046381A` transfer the whole record block from
`0x0048A250` (FND-AI-019).

The stores to the two planned target bytes (`0x0048A259`, `0x0048A25A`) lie
in the same functions. Outside the handlers and the dispatcher they are the
clears of `0x00458FA0` (`0x004591EF`, `0x00459209`) and `0x00409DE1`
(`0x00409EAF`, `0x00409EC9`), which store 0.

## Interpretation

A computer player plans only the actions in the value column. It never plans
a Bribe, a Give or a Sell, so no code of the original decides what the
planning record's target bytes hold for those three actions; the equipment
masks and recipient slot that a Give or Sell order carries exist only in the
gang record a human's order writes (FMT-STATE-001). Together with FND-AI-074,
which pairs each handler store with the target bytes stored after it, the
target bytes of a planning record are written only by `plan` (RULE-AI-004)
and cleared by the pass and the reset.

## Alternatives

The count covers the stores Ghidra resolves to `0x0048A258` itself. A store
through a pointer computed from another base would not appear in it; such
stores were not searched for.

## How to reproduce

List the references to `0x0048A258`, `0x0048A259` and `0x0048A25A`, keep the
`MOV` instructions whose destination operand holds the address, and group
them by containing function and source operand.
