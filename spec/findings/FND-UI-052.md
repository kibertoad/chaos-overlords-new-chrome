---
id: FND-UI-052
title: Item Information keeps its frame in a local that starts at 0 and steps once each time the animation flag is taken
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044B6AC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044C34B..0x0044C42C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044C475
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the Item Information handler `fn_0044B699` (FND-UI-013):

- `0x0044B6AC` sets the local `[ebp-0x134]` to 0 before anything is drawn.
- `0x0044C34B` calls `fn_004328F8(0)`, which takes and clears a timer flag
  (FND-COMLINK-010). When it returns nonzero, `0x0044C355` to `0x0044C36E`
  subtract 14 from the local when it is 14 or more and add 1 otherwise.
- `0x0044C3B4` to `0x0044C3DB` take the source rectangle with left
  `48 * frame`, top `0x161`, right `48 * frame + 48` and bottom `0x191` of
  surface 7, where the item's strip was loaded, and `0x0044C427` copies it to
  the screen rectangle `(162,141)-(210,189)` with `fn_0042773E`.
- The handler returns at `0x0044C475`.

## Interpretation

The item's picture shows frame 0 when the panel opens, and each time the
timer flag is taken the next of the 15 frames, after frame 14 frame 0. The
frame on screen is the value of the local when it was last copied, so a
capture of the panel shows the frame the local held at the last pass through
`0x0044C3B4`.

## Alternatives

None known.

## How to reproduce

In `0x0044B699`, find the store of 0 to `[ebp-0x134]` in the prologue, the
call of `0x004328F8` with 0, the compare of the local with `0xE`, the source
rectangle built from the local times 48 with `0x161` and `0x191`, and the call
of `0x0042773E` with 7 and 0.
