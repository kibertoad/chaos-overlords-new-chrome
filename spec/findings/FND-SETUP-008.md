---
id: FND-SETUP-008
title: A fifteen-frame spinner turns beside every network progress frame
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040CED0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040D72F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046A7CB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046D22F
tool: Ghidra 12.1.3
environment: null
---

## Observation

Every load of the 720-by-48 sheet `DATA/PX16/PX00138` sits beside a load of a
network progress frame: the transfer paths `0x0040CED0` and `0x0040D72F` also
load `PX00137`, and the connected-session paths `0x0046A7CB` and `0x0046D22F`
also load `PX00139`. No other function loads it.

Each path keeps a frame counter that runs from 0 to 14 and wraps to 0. On each
of its timed updates it copies the 48-by-48 cell for the current frame from
`PX00138` into the fixed rectangle from `(224,72)` up to but not including
`(272,120)`. On the synchronization path it runs alongside the six-seat bar
renderer. The normal local whole-turn resolver never loads or advances it.

## Interpretation

`PX00138` is a fifteen-frame activity animation for network transfers, not
command artwork.

## Alternatives

Cell `n` is taken to be the source rectangle `(48 * n, 0, 48, 48)`, since the
sheet is 720 pixels wide; the copy's source arithmetic is not recorded. The
period of the timed update is not recorded.

## How to reproduce

Search for loads of resource 138 through `0x00464108`. In each of the four
functions, find the counter compared with 15 (or 14) and reset to 0, and the
copy to the destination `(224,72)` with size 48 by 48.
