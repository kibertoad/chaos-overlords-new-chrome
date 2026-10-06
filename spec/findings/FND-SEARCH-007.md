---
id: FND-SEARCH-007
title: Every argument the city redraw and the site marker renderer take is pushed as a full dword with no leftover high bits
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00412900..0x004129C6
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004700DE
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004498F6
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00471D57
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004ABC84
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function extents are those of FND-EXE-004.

- At both calls of the marker renderer `fn_00412AC4` in the city redraw
  `fn_004123CC` (`0x0041295F` and `0x004129BE`, FND-SEARCH-006), the site's
  definition is loaded with `MOVSX EAX, byte ptr [... + 0x004A08EF]` and pushed
  from `EAX`, the sector and the ordinal are pushed from dword locals, and the
  controlled flag is pushed as the immediate `PUSH 1` or `PUSH 0`.
- `fn_004123CC` has two callers. The one at `0x004700DE` pushes the dword at
  `0x004ABC84`. The one at `0x004498F6` pushes its own first argument
  `[EBP+0x8]` as a dword, and the only call of its function, at `0x00471D57`,
  pushes the dword at `0x004ABC84`.
- `0x004ABC84` is written in five places, each a dword store. The redraw
  compares its first argument with the sector's owner at full width, with
  `CMP EAX, [EBP+0x8]`.

## Interpretation

No argument of either function carries leftover register bits. The definition
is sign-extended from its byte, which gives the byte's value for every
definition below 128, and the 22 definitions of FMT-DATA-001 are all below
that. A debugger that reads the four arguments of `fn_00412AC4` and the first
argument of `fn_004123CC` as dwords reads the values themselves.

## Alternatives

None known.

## How to reproduce

Read the pushes before the calls of `0x00412AC4` at `0x0041295F` and
`0x004129BE`, list the calls of `0x004123CC` and of the function that holds
`0x004498F6`, and list the references of `0x004ABC84`.
