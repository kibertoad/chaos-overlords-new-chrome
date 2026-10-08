---
id: FND-UI-061
title: The Combat Results and Last Turn Events handlers slide their panel in only on the branch that shows it, once per call
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00451F80
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00452146
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044F2FC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044F3D1
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004948F4
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function extents are those of FND-EXE-004.

- The Combat Results handler `fn_00451F80(viewer, flag)` first builds its page
  list: it stores 0 in the page count `g_004948F4`, then for each of the 64
  sectors where the viewer has a result row or a police flag and any player has
  a result row (the table at `0x004A8888`, FND-COMBAT-007), appends the sector
  to the page list at `0x00494AF0` and adds 1 to the count. When the count is
  0 it plays effect slot 4 if the flag is 0 and goes straight to its epilogue;
  nothing is drawn and no loop runs.
- Otherwise it loads resource 5012 into surface 7, draws the first page with
  `fn_00453087`, calls the panel-open helper `fn_0041953E(0)` (FND-UI-011) at
  `0x00452146`, and enters its event loop, which runs until the exit flag is
  set and is followed by the close helper `fn_004196F5`.
- The Last Turn Events handler `fn_0044F2FC(player)` counts the player's
  occupied report records (FND-EVENT-001). When the count is 0 it plays effect
  slot 4 and goes straight to its epilogue. Otherwise it loads resource 5010
  into surface 7, draws the page with `fn_0044FD6C`, calls `fn_0041953E(0)` at
  `0x0044F3D1`, and enters its event loop, which also ends with
  `fn_004196F5`.
- `0x00452146` and `0x0044F3D1` are the only calls of `fn_0041953E` in the two
  handlers, and neither lies inside the event loops.
- Both handlers are called from `fn_0046FD80` and `fn_004718EE`; neither handler
  calls the other.

## Interpretation

A call of either handler shows its panel exactly when it reaches its call of
the panel-open helper, and that call comes before the handler waits for input.
A call that returns without reaching it had nothing to show. Whether a panel
was shown can therefore be read from the call itself, whatever later input
closes it. Last Turn Events is a separate call of the caller, which in
`fn_0046FD80` lies after the call of Combat Results (`0x0047029D`, then
`0x00470356`), so between the two panels of one planning entry there is a
moment with no panel handler running.

## Alternatives

- Whether the two callers pass the same arguments in every case was not read;
  the reading holds for any arguments.

## How to reproduce

List the callers of `0x0041953E` and keep those inside `0x00451F80` and
`0x0044F2FC`. In each handler, read the counting loop before the branch on
the count, the branch that skips to the epilogue, and the loop that follows the
helper call. List the callers of both handlers.
