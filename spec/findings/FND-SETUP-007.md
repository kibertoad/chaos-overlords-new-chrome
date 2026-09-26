---
id: FND-SETUP-007
title: Two progress sheets are used only by the network transfer and synchronization paths
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
    address: 0x0040D3C0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046A7CB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046D22F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004726C0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046D77B
tool: Ghidra 12.1.3
environment: null
---

## Observation

`DATA/PX16/PX00137` is loaded only by `0x0040CED0` and `0x0040D72F`, both
reached through the network connection and setup paths. The 220-by-72 sheet
is copied to the screen rectangle from `(210,60)` up to but not including
`(430,132)`. Their shared helper `0x0040D3C0` copies a segment three pixels
high and as wide as the progress from the green strip of `DATA/PX16/PX00129`
at `(354,0)` into the frame at x 298, y 105, and selects one of the status
text pairs for modes 0 to 4 or 10 to 13.

`DATA/PX16/PX00139` is loaded only by `0x0046A7CB` and `0x0046D22F`, the
connected-session branches. `0x004726C0` takes those branches instead of the
normal whole-turn resolver when its network-session state is set. Their
shared renderer `0x0046D77B` scans all six connection slots. Each connected
slot with a positive progress value gets its own three-pixel green segment at
x 298, on row `96 + 4 * slot`; other slots are restored from the panel's empty
bar art. It selects status text from the same family. No local-game caller
reaches either renderer.

## Interpretation

`PX00137` is the progress frame of a network file transfer, and `PX00139` its
six-seat counterpart shown while the connected players' turns are exchanged.
Neither is a meter of the city or of a site.

## Alternatives

The source rectangle of the green strip is given only by its origin and its
three-pixel height. How the progress value maps to a width in pixels, and the
width of a full bar, are not recorded. Where the `PX00139` sheet is placed on
screen is not stated; it is taken to be the same centred rectangle as
`PX00137`, which has the same size.

## How to reproduce

Search the executable for loads of resources 137 and 139 through the image
loader wrapper `0x00464108`; the census finds only the four functions named.
In `0x0040D3C0` and `0x0046D77B`, find the copies from `(354,0)` of the
interface sheet to x 298, and the loop over six slots with the row
`96 + 4 * slot`.
