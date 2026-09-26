---
id: FND-GANG-008
title: The Hire input handler opens the gang information panel PX05000 for an offer with a record whose Force is 0, so the panel shows two question marks for Force
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00416CC6..0x00416DE4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00416F7B..0x00417095
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges of the functions named here are in FND-EXE-004.

The Hire input handler `fn_00416C75` takes a pointer position and an event
code. It picks an offer slot from the position's x less `0x1B8`: slot 0 up to
`0x40`, slot 1 up to `0x82`, slot 2 beyond (`0x00416C8C..0x00416CB4`). When
the event code is 2 it builds a 32-byte gang record on its stack in one of two
places, depending on the byte at `0x004ABC60` (`0x00416CBE`):

- `0x00416CC6..0x00416D72`, and `0x00416F7B..0x0041701D` (the latter under a
  further test of the first argument at `0x00416E04`);
- in both, sector byte 100, definition byte from `hire_offers` at
  `0x004ABBC0 + active_player * 3 + slot`, Force byte 0, the three item bytes
  -1, and the fourteen statistic bytes copied from the definition fields
  `0x7E`, `0x80`, `0x84` and `0x86` to `0x9A`.

It passes that record through the statistics rebuild `fn_0047781F`
(`0x00416DA7`, `0x00417052`) and then calls the live-gang panel handler
`fn_00449E80`, which loads `PX05000`, with the rebuilt record (`0x00416DE4`,
`0x00417095`). These are two of the four calls of `fn_00449E80`; the other two
are in `fn_00414D8C` (`0x00416797`) and `fn_004169B3` (`0x00416A86`).

## Interpretation

An offered gang can be shown on the same information panel as a hired one.
Its record has Force 0, so the panel draws two question marks for Force
(FND-GANG-006). Its sector byte is 100, so the rebuild adds no site bonuses,
and with no weapon its Combat includes Strength, Fighting and Martial Arts
(FND-GANG-007): the panel shows the offer's statistics as they would be for
the gang bare handed, without items or sites.

## Alternatives

- Which input produces event code 2, and what the test at `0x00416E04` checks,
  were not identified; the event is taken to be the one that opens the offer's
  details.
- Whether `fn_00414D8C` or `fn_004169B3` can also pass a record with Force 0
  was not checked.

## How to reproduce

List the calls of `fn_00449E80`. In `fn_00416C75`, the two calls follow a
store of 100 into the record's sector byte and of 0 into its Force byte, the
copy of the definition fields at `0x004A287E`, `0x004A2880`, `0x004A2884`,
`0x004A2886` and `0x004A2888`, and a call of `fn_0047781F`.
