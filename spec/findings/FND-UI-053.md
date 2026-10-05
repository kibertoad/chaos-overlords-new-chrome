---
id: FND-UI-053
title: The Sell and Give panels turn their item pictures with a frame local that starts at 0, as Item Information does
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00443BD1
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004451AF..0x004452A1
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00445654
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00445A63
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00447635..0x00447678
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00447ADA
tool: Ghidra 12.1.3
environment: null
---

## Observation

The Sell handler `fn_00443BBD` (FND-SELL-001) and the Give handler
`fn_00445A4F` (FND-GIVE-001) each keep a frame in a local:

- Sell sets `[ebp-0x28]` to 0 at `0x00443BD1`. When `fn_004328BE(0)` at
  `0x004451B1` reports the timer flag, it takes the flag with
  `fn_004328F8(0)` and, at `0x004451CF` to `0x004451E2`, subtracts 14 from the
  local when it is 14 or more and adds 1 otherwise. From `0x004451E5` it
  copies, for each filled item slot, the source rectangle with left
  `48 * frame` and width 48 of the slot's strip, the first at `0x00445299`.
  It returns at `0x00445654`.
- Give sets `[ebp-0x2c]` to 0 at `0x00445A63` and steps it the same way after
  `fn_004328BE(0)` at `0x00447637` and `fn_004328F8(0)`, at `0x00447655` to
  `0x00447668`, before its copies from `0x0044766B`. It returns at
  `0x00447ADA`.

## Interpretation

Both panels show their item pictures at frame 0 when they open and step all of
them together, one frame each time the timer flag is taken, frame 0 after
frame 14, as Item Information does (FND-UI-052). A capture of either panel
shows the frame its local held at the last copy.

## Alternatives

None known.

## How to reproduce

In `0x00443BBD`, find the store of 0 to `[ebp-0x28]` in the prologue, the
calls of `0x004328BE` and `0x004328F8` with 0, the compare of the local with
`0xE` and the source rectangles built from the local times 48. Do the same in
`0x00445A4F` for `[ebp-0x2c]`.
