---
id: FND-UI-070
title: The detailed sector screen draws a site's progress meter only when the sector's owner is the active player
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00410BB2..0x00410BC6
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00410CD6
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the site loop of the sector-view compositor `fn_00410770` (FND-UI-018,
step 6), after the frame is overlaid on site portrait `k` with the call at
`0x00410BAA` (FND-UI-026):

- `0x00410BB2` to `0x00410BB8` load the selected sector's `owner` byte, the
  signed byte at `0x004A08E8 + sector * 0x24` (FMT-STATE-002), with the sector
  taken from the compositor's first argument.
- `0x00410BC0` compares it with the 32-bit `active_player` at `0x004ABC84`.
- `0x00410BC6`, a `JNZ`, jumps to `0x00410CD6` when they differ. The code it
  skips reads the site's definition and `progress`, computes the meter length
  of FND-UI-036 and copies the green strip; `0x00410CD6` goes on to copy the
  portrait to the back buffer.

The test reads neither the compositor's second argument, the player whose
gangs the cards list, nor any visibility byte. Inside the skipped code a
percentage of 0 skips the copy as well, so a site with no progress shows the
bare red track to the owner too.

## Interpretation

Only the sector's owner sees how far its sites have been influenced, and only
while that owner is the active player. A sector with no owner (owner -1) never
shows a meter, since `active_player` is never negative. Showing another
player's cards through an Overlord portrait (FND-UI-015) does not change it.

## Alternatives

- None. The jump target and the skipped range are read from the listing, not
  from the decompiler's reconstruction.

## How to reproduce

In `0x00410770`, find the `MOVSX` of `[EAX + EAX*0x8 + 0x4A08E8]` at
`0x00410BB8` after the call to `0x00427864` at `0x00410BAA`, the
`CMP EAX, [0x004ABC84]` at `0x00410BC0` and the `JNZ 0x00410CD6` after it.
