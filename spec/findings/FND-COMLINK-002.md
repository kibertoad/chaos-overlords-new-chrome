---
id: FND-COMLINK-002
title: Comlink View opens at the first unread message, refuses an empty inbox, and pages with bounded Previous and Next controls
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045D61A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004718EE
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The View handler `fn_0045D61A` is called only from the upper (View) half of
  the Comlink control in the main-console dispatcher `fn_004718EE`. It loads
  `PX05017` and copies it to the standard panel position.
- It reads the active player's count at `0x004981E0 + player * 4`. With a
  count of 0 it plays general-effect slot 4 and returns without opening the
  panel.
- Before opening, it scans the player's 16 records at
  `0x0049CA90 + player * 0xA60` in ascending order and sets the cursor to the
  first record whose occupied byte is set and whose read byte is clear. When no
  record is unread, the cursor keeps its value.
- The Left (`0x25`) and Right (`0x27`) virtual keys step one message back or
  forward through the bounded page helper, which refuses a step before the
  first or after the last message with slot 4. Enter (`0x0D`) and Execute
  (`0x2B`) press the dismiss control.
- Pointer releases are first limited to the panel, `(104,124)-(448,333)`, then
  converted to panel-local coordinates. The half-open controls are Previous
  `(31,33)-(57,56)`, Next `(59,33)-(85,56)` and dismiss `(33,169)-(82,191)`.
- Each page change draws the message again and copies the region
  `(104,124)-(448,333)` to the screen.
- The handler has no branch for Up, Down or Backspace.

## Interpretation

View starts at the oldest unread message kept, and at the message last shown
when every message has been read. The arrows are 26-by-23 controls two pixels
apart, and the dismiss control is 49 by 22.

## Alternatives

None known.

## How to reproduce

From the Comlink tile's branch in `fn_004718EE`, follow the upper-half call to
`fn_0045D61A`. Read the count test that calls the sound wrapper with slot 4,
the unread scan, the virtual-key comparisons and the rectangle constants.
