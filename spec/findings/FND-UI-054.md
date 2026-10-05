---
id: FND-UI-054
title: The idle gang warning starts its blinking line shown and counts its six and two ticks from the open
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044872C..0x00448730
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00448C73..0x00448D51
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00448E30
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the idle gang warning handler `fn_00448718` (FND-OPTIONS-002, FND-UI-024):

- `0x0044872C` sets the byte `[ebp-0xc]` to 1 and `0x00448730` sets the
  dword `[ebp-0x2c]` to 6, before the panel is drawn.
- On a pass where `fn_004328F8(0)` reports a tick of slot 0, `0x00448C73`
  decrements `[ebp-0x2c]` and goes on only when it reaches 0. Then, when
  `[ebp-0xc]` is not 0, `0x00448C89` clears it, `0x00448C8D` sets the count to
  2 and the code that follows fills the line black; otherwise `0x00448D4D` sets
  the byte to 1, `0x00448D51` sets the count to 6 and the code that follows
  copies the line back.
- `0x00448E30` is the handler's `leave`.

## Interpretation

The byte says whether the line is shown and the dword how many ticks of that
part are left. The line is shown when the panel opens, for six ticks, then
black for two, and so on, so after `t` ticks of slot 0 since the open it is
shown when `t % 8` is below 6. While the line is shown, `t % 8` is 6 less the
count; while it is black, 8 less the count.

## Alternatives

None known.

## How to reproduce

In `0x00448718`, find the stores of 1 to `[ebp-0xc]` and 6 to `[ebp-0x2c]`
at its start, the `dec` of `[ebp-0x2c]` after the call of `0x004328F8`, and
the two branches that store 2 and 6 into it.
