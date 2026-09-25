---
id: FND-SETUP-017
title: The setup reset gives every slot the name string 61 plus its number, one human in slot 0 with portrait 0, and portrait 15 to the empty slots
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00410016..0x0041012C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00490630..0x00490697
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_00410016` (range from FND-EXE-004) takes no arguments and writes the setup
roster:

- For each slot `p` from 0 to 5 it clears the 12-byte name record at
  `0x00490630 + 12p`, loads string resource 61 into it through the string
  loader `fn_00466673` with a limit of 8 and its counted form (the first byte
  holds the length), then increments the length byte and stores the digit
  `'1' + p` at the new last position with a NUL after it.
- It stores 0 in the dword at `0x00490680` and -1 in the dwords for slots 1 to
  5 (`INT32LE[6]`).
- It stores 0 in the byte at `0x00490678` and 15 in the bytes for slots 1 to 5.
- It stores 0 in `0x004854C4`, the selected card (FND-SETUP-005).

Its three callers run it before a setup screen opens: the title loop
`fn_00460CCF` at `0x004614A3`, the local setup `fn_0040B9C0` at `0x0040BA0F`
and the network setup `fn_004677F0` at `0x00467832`. Apart from this reset,
the only reader and writer of `0x00490680` and `0x00490678` is the local setup
handler `fn_0040E0A0`, at `0x0040E261`, `0x0040E275`, `0x0040EA0B` and
`0x0040EA1E`.

## Interpretation

`0x00490680` is the setup screen's controller list and `0x00490678` its
portrait list. The screen opens with one human (controller 0) in slot 0 with
portrait 0, and the other five slots empty (controller -1) with portrait 15,
one past the last of the fifteen portraits. Every slot starts with the same
default name, string 61 followed by the slot's number from 1 to 6, whatever
its portrait; the portrait-based names of FND-SETUP-002 come from a later
step.

## Alternatives

- Where the setup lists are copied into `controller` (`0x004AB638`) and the
  match's portrait list when a game begins has not been read.

## How to reproduce

In `0x00410016`, find the call to `0x00466673` with 61 and 8, the addition of
`'1'`, and the stores of 0, -1 and 15 to `0x00490680` and `0x00490678`. List
the references to both addresses.
