---
id: FND-UI-027
title: The sector value renderer draws Income and Tolerance with no owner test
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004120EF..0x004123CB
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges of the functions named here are in FND-EXE-004.

In the field renderer `fn_004120EF(sector, flag)` the calls run in a straight
line from the sector name to the Cash row:

- the Income row, the string resource `0x11` plus sector byte 4, cut to two
  characters, drawn at `(568,69)` (`0x00412172`, `0x004121AE`);
- the Tolerance row, sector byte 5, drawn by `fn_00414187` at `(568,78)`
  (`0x004121F1`);
- the Support row, sector byte 6 when the sector's owner byte equals the
  active player at `0x004ABC84` (`0x00412211`) and 0 otherwise, at `(568,87)`;
- the Cash row, sector byte 3 under the same comparison (`0x00412298`) and 0
  otherwise, at `(568,96)`.

No branch comes before the Income and Tolerance calls, and the only two
comparisons with the active player are the ones guarding Support and Cash. The
second argument, when nonzero, copies the value area `(568,60)-(580,103)` to
the screen (`0x0041231E`).

## Interpretation

Every player sees the Income and Tolerance of any selected sector; Support and
Cash show 0 unless the viewer owns it. This settles the question FND-UI-035
left open.

## Alternatives

None known.

## How to reproduce

In `fn_004120EF`, list the reads of `0x004A08EB` to `0x004A08EE` indexed by the
sector times `0x24` and the two compares with `0x004ABC84`; note that the reads
of offsets 4 and 5 come before the first compare.
