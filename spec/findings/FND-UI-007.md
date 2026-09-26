---
id: FND-UI-007
title: The About menu command shows PX00100 full screen in a blocking loop until a click, key or window event
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00462579
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00464D53
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The message dispatcher `fn_00462579` calls `fn_00464D53` only for the command
  event `(0x80, 3)`. The menu resource `Chaos Overlords.exe#MENU/101` gives that
  command to the last item of its Help menu, About Chaos Overlords, marked
  `MF_END` (`0x0080`) with command ID `0x8003` (FND-UI-008).
- `fn_00464D53` first releases the normal screen surfaces, loads resource 100
  (`PX00100`) into surface 3 at its full size of 640 by 460, and copies it
  opaquely over the whole canvas.
- Its own message loop ends on the mouse, key and window messages its event
  layer numbers 2, 3, 4, 17 and 18. Paint messages redraw the same surface.
- On exit it reloads surface 3 with resource 3000 and gives the screen surfaces
  back to their previous owners.

## Interpretation

`PX00100`, the publisher and developer credits, is a screen of its own that
blocks the game until the player clicks or presses a key. It is reached only
from Help, About Chaos Overlords, on the menu bar, not from the title screen.

## Alternatives

- Which window messages the numbers 2, 3, 4, 17 and 18 stand for is read from
  the event layer's own numbering; the mapping to Windows messages has not been
  written down.

## How to reproduce

In `0x00462579`, find the branch that compares the command's high byte with
`0x80` and low byte with 3, and follow its call to `0x00464D53`, which passes
100 and then 3000 to the image loader `0x00464108`.
