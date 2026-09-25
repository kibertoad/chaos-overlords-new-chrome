---
id: FND-POLICE-004
title: Police presence is added only by the third Crackdown in the window, the one that neutralizes the sector
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047367C..0x004737D3
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A08F7
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the Chaos sector pass of `fn_00472775`, a sector that triggers sets a local
flag to 1 at `0x0047367C`. If the first history slot (`0x004ABCC0 + sector *
4`) is -100, it receives `elapsed_turns` and the flag is cleared
(`0x004736A5`, `0x004736AD`). If the second slot (`0x004ABCC2 + sector * 4`)
is -100 and the flag is still 1, the second slot receives `elapsed_turns` and
the flag is cleared (`0x004736E6`, `0x004736EE`).

At `0x004736FF` the code jumps to `0x004737D3`, the end of the sector's
iteration, when the flag is 0. Only when the flag is still 1, that is when
neither slot was free, does it fall through to: the report to the owner
(call at `0x00473724`), owner -1 at `0x00473735`, the three progress bytes
set to 0 (`0x00473746`, `0x00473757`, `0x00473768`), both history slots set
to `elapsed_turns` (`0x0047377B`, `0x0047378E`), the call to `fn_0045D227`
with 3 at `0x004737A9`, and the store of the old presence byte + the draw + 2
into the sector's presence byte (record offset `0x0F`) at `0x004737BF`.

The presence byte `0x004A08F7 + sector * 0x24` is written by four
instructions in the whole program: `0x004737BF` above, the decrement at
`0x00475E74`, the new-game initializer at `0x0046E74A` (100) and the city
generator at `0x004760E4` (0).

## Interpretation

A Crackdown that fills a free history slot makes no draw and adds no police
presence: it zeroes the Chaos results and sends its reports (FND-POLICE-002)
and nothing else. Only the third Crackdown within the window neutralizes the
sector, and only that one adds 3 to 5 turns of police presence, with one
`roll(3)`. Police presence therefore always arrives together with the loss of
the sector.

This contradicts FND-POLICE-001, which places the draw after the history
block for every Crackdown. The jump at `0x004736FF` skips the draw whenever a
slot was free.

## Alternatives

None for the control flow; the branch and its target are unambiguous. Whether
the flag byte can be set by anything else between `0x0047367C` and
`0x004736FF` was checked: only the two clears write it there.

## How to reproduce

In `fn_00472775`, go to `0x0047367C` and follow the two history tests to the
test of the flag at `0x004736FD`; its conditional jump at `0x004736FF` goes to
`0x004737D3`, which is after the call at `0x004737A9` and the store at
`0x004737BF`. List the references to `0x004A08F7` to see the four writes.
