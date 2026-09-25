---
id: FND-RESEARCH-005
title: The Research panel's Enter, Execute and Escape draw the pressed confirm and Cancel faces before they act
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004427FA..0x004437E6
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the Research handler `fn_004427FA` (FND-RESEARCH-004), the key-down case
of the event switch:

- Enter (`0x0D`) or Execute (`0x2B`) with a row selected calls
  `fn_00418CCC(0, ...)` on the confirm face `(137,293)-(187,316)`
  (`0x00442B2D`) and writes the order; with no row selected it plays slot 4.
- Escape (`0x1B`) calls `fn_00418CCC(1, ...)` on the Cancel face
  `(137,261)-(187,284)` (`0x00442BC9`) and ends without an order.

The Influence handler `fn_0043F692` makes the same two calls at `0x00440868`
and `0x004408FD`, as FND-INFLUENCE-003 records.

## Interpretation

As on the Equip, Influence, Move, Give and Sell panels, the keys play slot 3,
show the pressed face for one tick of the presentation clock (FND-UI-019,
RULE-TIMER-004) and then act.

## Alternatives

None known.

## How to reproduce

In `0x004427FA`, find the key test of `0x2B` and `0x0D` with the call to
`0x00418CCC` with 0 and the rectangle `(0x125,0x89,0x13C,0xBB)`, and the test
of `0x1B` with the call with 1 and `(0x105,0x89,0x11C,0xBB)`, both in
`(top,left,bottom,right)` order. List the callers of `0x00418CCC`.
