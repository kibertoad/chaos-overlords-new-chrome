---
id: FND-SETUP-006
title: The three legacy network setup screens are separate flows with their own seat editors and controls
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00460CCF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004677F0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040B9C0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00456F80
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00457B7B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00457EED
tool: Ghidra 12.1.3
environment: null
---

## Observation

The title loop `0x00460CCF` calls three unrelated handlers directly:
`0x004677F0`, `0x0040B9C0` and `0x00456F80`. Each belongs to a network
session path.

`0x004677F0` loads `DATA/PX16/PX00144`, starts host-side channels, and then
shows a message giving the host's address. It starts with one configured seat,
edits at most four, and keeps polling the participants' state before it
allows the game to start. Its four 64-by-68 seat cells are at `(397,94)`,
`(480,94)`, `(397,168)` and `(480,168)`. Its four vertical action rectangles
are `(254,371,24,92)`, `(254,468,24,92)`, `(375,370,45,92)` and
`(375,468,45,92)`, four numbers each in the order the analysis recorded them
(see Alternatives for how to read them).

`0x0040B9C0` first negotiates one of three connection modes and then loads
`DATA/PX16/PX00145`. It edits four seats at `(251,124)`, `(334,124)`,
`(251,198)` and `(334,198)`. Its action rectangles are `(284,225,24,92)`,
`(284,322,24,92)`, `(345,224,45,92)` and `(345,322,45,92)` in the same form.

Both editors use the same 16-pixel left, 15-pixel right and 10-pixel bottom
portrait and name bands as the local setup (FND-SETUP-005), but write the
network session's seat state. Each has its own four-case held-button helper
that shows the pressed image only while the pointer stays inside the
rectangle, plays the push cue (sound slot 2) on the press, and plays the
rejection cue (slot 4) after it when an operation on the seat count is
refused.

`0x00456F80` loads `DATA/PX16/PX00146`, draws a six-seat strip through its own
unscaled renderer `0x00457B7B`, and keeps processing connection records. Its
two controls are `(230,224,45,92)` and `(230,322,45,92)` in the same form:
the upper one continues the session, the lower one closes all twelve tracked
connections and returns. Helper `0x00457EED` gives them the held and
release-inside behaviour, with the normal image from surface 1 and the pressed
image from surface 7.

## Interpretation

`PX00144` is the network host's lobby, `PX00145` a compact editor for a client
after its connection opens, and `PX00146` the view that waits for every
participant to be ready. None of them is a variant of the local setup screen
`Px00143` chosen by player count.

## Alternatives

The order of the four numbers in each action rectangle is not settled. Read
as `(x, y, width, height)`, the `PX00146` pair is two tall 45-by-92 buttons one
above the other, which fits "upper" and "lower", and the `PX00145` set is two
columns of such buttons; but two of the `PX00144` rectangles would then start
at y 468 and one would end at y 463, below the 460-pixel image. Read as
`(y, x, height, width)`, the `PX00144` set becomes 92-wide buttons 24 and 45
pixels high at x 370 or 371 and x 468, y 254 and 375, the same shapes as the local
setup's Add, Remove, Begin and Cancel buttons. Which of the actions does what,
other than the two `PX00146` controls, is not recorded.

## How to reproduce

From `0x00460CCF`, follow the three direct calls. In each handler find the
image load through wrapper `0x00464108` with the resource number, the tables
of seat origins, and the rectangle constants passed to the held-button
helpers.
