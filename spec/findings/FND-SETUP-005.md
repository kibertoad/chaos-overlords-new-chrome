---
id: FND-SETUP-005
title: The local setup screen's six player cards take input over broad bands, select before they act, and swap on drag
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040E0A0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00468D87
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00468CFC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040F63D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040F72E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004854C4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040EE8A
tool: Ghidra 12.1.3
environment: null
---

## Observation

Hit cells. The full local setup handler `0x0040E0A0` builds six card origins,
`(397,94)`, `(480,94)`, `(397,168)`, `(480,168)`, `(397,242)` and
`(480,242)`, for slots 0 to 5, and tests a half-open 64-by-68 rectangle at each.
Inside an occupied human card, a press with vertical offset below 58 works the
portrait: horizontal offset below 16 calls the decrement helper `0x00468D87`,
and horizontal offset greater than 48 calls the increment helper `0x00468CFC`.
Vertical offset 58 to 67 calls the name editor `0x0040F63D`. The input bands
are therefore 16 by 58 on the left, 15 by 58 on the right and 64 by 10 along
the bottom, larger than the arrows and the name text drawn on the card.

Name editor. `0x0040F63D` clears its buffer, opens dialog resource `0x8B`
(`Chaos Overlords.exe#DIALOG/139`) and on acceptance copies the buffer into the
player's name record only when it is not empty. Accepting an empty editor
leaves the name as it was. It accepts at most ten characters.

Drag. Before acting on a press, the helper `0x0040F72E`, called only from the
setup handler, waits while the pointer stays within the half-open box from -2
to +1 pixels of the press point on each axis. If the pointer leaves that box
with the button still down, a drag begins. The helper draws a 40-by-40
portrait token centred on the pointer, keeps the token's centre within x 20 to
620 and y 20 to 440, and on release tests the same six card rectangles. A
release over a card swaps the complete type, portrait and 12-byte name record
of the two slots, including an empty one. A release outside every card
changes nothing. The press actions above run, from the press point, only when
the helper reports that no drag began.

Selection. The global selected-card index at `0x004854C4` starts at 0. A press
on another occupied card only selects it and redraws; the arrows and the name
editor act only on the card that was already selected. Add selects the newly
occupied slot. Remove deletes the selected slot and selects the highest
occupied slot that remains. A successful drag selects its destination.

Drawing. The renderer `0x0040EE8A` copies every 32-by-32 portrait of the top
strip opaquely. For each occupied card it copies source `(32 * portrait, 480,
32, 30)` of the interface sheet, scaled opaquely to `(cardX, cardY + 3, 64,
60)`. Only the selected card then receives the arrow overlay, source
`(220,138,64,62)` of `DATA/PX16/PX00140`, keyed on exact white, at the card's
origin. The branch for cards that are not selected asks for copy mode 0, but
because its source and destination sizes differ, the copy wrapper takes its
opaque scaling path before it looks at the mode.

## Interpretation

Where the setup screen draws its controls and where it takes input are
different rectangles. A card must be selected before its arrows or name can
be used, and dragging a face moves a player's whole identity to another colour
slot or exchanges it with another player's.

## Alternatives

The card origins the renderer draws at (`cardX`, `cardY`) are not given as
numbers. What the portrait helpers do at the ends of the range (wrap, skip
portraits already taken) is not recorded. What a press on an empty card does,
and whether a drag can start from one, is not recorded. The interface sheet
the portraits come from is taken to be `DATA/PX16/PX00129`, whose row y=480
holds sixteen 32-by-32 portraits.

## How to reproduce

In `0x0040E0A0`, find the six origin pairs and the 64 and 68 extents, the
tests against 58, 16 and 48, and the calls to `0x00468D87`, `0x00468CFC` and
`0x0040F63D`. `0x0040F72E` has this handler as its only caller; find its loop
comparing the pointer against the press point minus 2 and plus 2, the clamps
at 20, 620 and 440, and the record swap. `0x0040F63D` loads dialog `0x8B`.
`0x0040EE8A` contains the scaled portrait copy and the `(220,138)` overlay.
