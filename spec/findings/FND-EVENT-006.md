---
id: FND-EVENT-006
title: The event pump blinks the Events, Comlink and a third light together, lit for two presentation ticks and dark for two
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00462EE9..0x004633DC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00463713..0x00463787
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487804..0x00487824
tool: Ghidra 12.1.3
environment: null
---

## Observation

Screen coordinates are window coordinates (slot 0). Rectangles are given as
x..x, y..y, end exclusive.

- The event pump `fn_00462579(event, consume)` runs its timed block when
  `0x00487830` is set and `fn_004328BE(0)` reports a pending timer-0 tick
  (`0x00462EE9`). At the end of the block (`0x0046377D`) it clears the tick
  with `fn_004328F8(0)` only when `consume` is nonzero. The planning loops
  `fn_0046FD80` (`0x0047039D`) and `fn_00471F06` (`0x00472109`) and the
  Comlink View handler `fn_0045D61A` (`0x0045D755`) pass 1.
- The block tests bit 0 of the counter `0x00487804` (`0x00462F45`). When the
  bit is 0 it flips the phase byte `0x00487824`; nothing in this part runs on
  an odd count.
- Phase byte 0 (the byte becomes 1, `0x0046319B`), three independent tests:
  - `0x0048780C` set: copy `PX00129` (surface 6) x 488..496, y 512..528 to
    the screen at x 592..600, y 282..298 and set `0x00487810`.
  - `0x00487814` set (`0x00463267`): the same source rectangle to x 540..548,
    y 126..142, and set `0x00487818` (`0x00463314`).
  - `0x0048781C` set (`0x00463322`): the same source rectangle to x 592..600,
    y 126..142, and set `0x00487820` (`0x004633CF`).
- Phase byte 1 (the byte becomes 0, `0x00462F61`): for each of `0x00487810`,
  `0x00487818` and `0x00487820` that is set, copy the same screen rectangle
  from surface 1 to the screen and clear the byte (`0x0046301F`, `0x004630D7`,
  `0x0046318F`).
- `0x00487810`, `0x00487818` and `0x00487820` have no other references.
  `0x00487824` has no reference outside the pump, so its starting value is
  the image's.
- After the lights and the selected-sector frame, the block adds 1 to
  `0x00487804` and sets it to 0 when it reaches 8 (`0x00463713..0x00463726`);
  on that wrap it advances `0x00487808` modulo 3 and plays sound slot 6 when
  the result is 0 and `0x0048781C` is set (`0x00463730..0x00463770`).
- The pump reads `0x00487814` only at `0x00463267`.

## Interpretation

Each light is an 8-by-16 lamp image drawn straight onto the window over its
control, and its "off" state is the control as composed on surface 1. The
lamps change only on even counts, and alternate between lit and restored, so
a lamp whose flag stays set is lit for two ticks of timer 0 and dark for two:
a blink with a period of four ticks, two thirds of a second at six ticks per
second (FND-UI-001). The Events light is `0x00487814`, the Comlink light
`0x0048781C`; all lit lamps change on the same tick. When a flag is cleared
while its lamp is lit, the next dark step still restores the control, so the
lamp goes out within two ticks. When a flag is set during a dark phase, the
lamp lights on the next lit step.

The counter `0x00487804` is the one that paces the Comlink alert repeat
(FND-AUDIO-012) and the selected-sector frame (FND-UI-017); it wraps at 8
exactly as stored. The third lamp, at (592, 282), follows `0x0048780C`, which
the match function sets while it runs the planning loop for a surviving
player at the end of the match (FND-OBJECTIVE-004); which control sits under
it was not read.

## Alternatives

- A caller that passes 0 for `consume` leaves the tick pending, and the next
  call runs the block again; the blink is then faster than stated. None of the
  three callers above does this; the other callers were not all checked.
- A repaint from surface 1 between two steps removes a lit lamp until the next
  lit step, which shortens that flash.

## How to reproduce

In `fn_00462579`, read the test of `0x00487830` and the timer-0 test at
`0x00462EE9`, the bit test at `0x00462F45`, the three copies from surface 6
after `0x0046319B` and the three copies from surface 1 after `0x00462F61`, and
the counter update at `0x00463713`. List the references to `0x00487804`,
`0x00487810`, `0x00487814`, `0x00487818`, `0x0048781C`, `0x00487820` and
`0x00487824`. Read the push of 1 before the calls at `0x0047039D`,
`0x00472109` and `0x0045D755`.
