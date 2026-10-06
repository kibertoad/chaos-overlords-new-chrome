---
id: FND-UI-046
title: The pointer hold loops of the console tiles, the held-button helper and the event page arrows never reach the event pump, and no hold loop touches timer slot 0
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00419446
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00418BD4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004517BC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00415E7D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00415F2D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00415F9F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00417570
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00417620
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00417692
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043BD69
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043BE35
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047108B
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function extents are those of FND-EXE-004. The pointer record is the one
`fn_00465B64` copies out (FND-UI-044): the byte at offset `0xE` is set while
the left button is down and the byte at offset `0xF` while the right one is.

- The main-console helper `fn_00419022` (FND-UI-032) plays slot 2 and then
  loops while the byte for its second argument's button is set, offset `0xE`
  when the argument is 0 and `0xF` otherwise. Each pass calls `fn_0045C2CD`
  (`0x00419446`) and reads the record again.
- The held-button helper `fn_00418821` loops while the left-button byte is set,
  calling `fn_0045C2CD` on each pass (`0x00418BD4`). Its callers include the
  attack picker's two faces (`0x0043BD69`, `0x0043BE35` in `fn_0043B290`), the
  sector view's back control (`0x0047108B` in `fn_00470E24`), the two Hire
  rejection paths, and the Cancel, confirm, Done and close faces of the
  command, information, Comlink Send and Last Turn Events panels.
- The Last Turn Events page helper `fn_00451602` (FND-EVENT-005), for a pointer,
  loops while the left-button byte is set and calls `fn_0045C2CD` on each pass
  (`0x004517BC`). Only its key path waits through `fn_00464CD9(1)`, which calls
  the event pump `fn_00462579`.
- The 41 direct calls of `fn_00462579` lie in 40 functions, none of them
  `fn_00414D8C`, `fn_00416C75`, `fn_00418821`, `fn_00419022` or `fn_00451602`.
  No chain of direct calls of up to eight steps leads from `fn_00418821` or
  `fn_00419022` to the pump, or to `fn_004328F8`, which clears a timer slot's
  flag (FND-UI-023). The chains from `fn_00414D8C` and `fn_00416C75` to the
  pump go through the panels they open after a hold, `fn_0043F692` and
  `fn_00449E80`.
- The hold loops of `fn_00414D8C` and `fn_00416C75` read and clear timer slot
  1 only: each pushes 1 for its clear before the drag loop (`0x00415E7D`,
  `0x00417570`), its read in the loop (`0x00415F2D`, `0x00417620`) and its
  clear after the image moves (`0x00415F9F`, `0x00417692`).

## Interpretation

While a console tile, a held-button face, an event page arrow, an offer or a
gang card's portrait is held under the pointer, the game runs a loop that
dispatches window messages and never calls the pump. Every step the pump takes
on a presentation tick stops for the hold: the planning bar and its warning
sounds, the Comlink blink step with the lights, the selection frame and the
alert repeat that follow it, and the music poll. No hold loop clears timer
slot 0, so its flag, set by the first tick that falls during the hold, is still
set when the hold ends, and the pump takes that one tick on its first call
after it. The other ticks of the hold are lost, and the steps the pump drives
stay that many ticks behind the timer from then on.

A panel that animates on slot 0 in its own loop (FND-UI-023) is stopped by a
hold in the same way; this finding does not record what such a panel does when
the hold ends.

## Alternatives

None known.

## How to reproduce

List the references to `0x00462579`, `0x0045C2CD` and `0x004328F8`, and the
callers of `0x00418821`. In `0x00419022`, `0x00418821` and `0x00451602`, find
the loop around the call of `0x0045C2CD` and the record byte it tests. At
`0x00415E7D`, `0x00415F2D`, `0x00415F9F`, `0x00417570`, `0x00417620` and
`0x00417692`, read the slot pushed before the call. Look for call paths from
`0x00418821` and `0x00419022` to `0x00462579` and `0x004328F8`.
