---
id: FND-UI-047
title: The panels that animate on timer slot 0 take the flag after their event switch, so a held face stops the animation and the release pass takes one tick
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044C337
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044C04F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044FC3C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044F774
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044F8AB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044FA24
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004451B1
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00447637
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044B3FF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045FD4D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004310EA
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00430FA1
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00448C55
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00442A6E
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function extents are those of FND-EXE-004. `fn_004328BE(0)` reads the flag of
timer slot 0 and `fn_004328F8(0)` clears it (FND-UI-023). The held-button
helper `fn_00418821` and the Last Turn Events page helper `fn_00451602` loop
until the left button comes up without calling either function or the event
pump `fn_00462579` (FND-UI-046).

Eight functions that call `fn_00418821` also read slot 0 themselves. Each runs
one loop whose pass calls the pump, dispatches the event it returns through a
switch whose pointer cases call the helpers, and then, after the switch and in
the same pass, reads slot 0; when the flag is set it clears it and takes one
step of its animation. None of them reads or clears slot 0 anywhere else in the
loop.

| Function | Panel | Pump call, second argument | Slot 0 read, clear | Held-face calls | Step |
|---|---|---|---|---|---|
| `fn_0044B699` | Item Information | `0x0044BE43`, 0 | `0x0044C337`, `0x0044C34D` | `fn_00418821` at `0x0044C04F` (exit) | Rotation counter 0 to 14, wrapping, and the frame copied to the screen |
| `fn_0044F2FC` | Last Turn Events | `0x0044F3F1`, 0, while `g_004948F8` is set; `0x0044F43C`, 1, otherwise | `0x0044FC3C`, `0x0044FC61`, taken only while `g_004948F8` is set | `fn_00418821` at `0x0044FA24` (exit); `fn_00451602` with a pointer at `0x0044F774` and `0x0044F8AB` (Previous, Next) | Research item counter 0 to 14 |
| `fn_00443BBD` | Sell | `0x00444568`, 0 | `0x004451B1`, `0x004451C7` | `fn_00418821` at `0x00444BE5` and `0x00444CC4` (Cancel, Sell) | One counter 0 to 14 for the three item pictures |
| `fn_00445A4F` | Give | `0x004461CB`, 0 | `0x00447637`, `0x0044764D` | `fn_00418821` at `0x00446D7D` and `0x00446E42` (Cancel, Give) | One counter 0 to 14 for the item pictures |
| `fn_00449E80` | Gang information | `0x0044ACFC`, 0 | `0x0044B3FF`, `0x0044B415` | `fn_00418821` at `0x0044AED6` and `0x0044B016` (close) | One counter 0 to 14 for the three item pictures |
| `fn_0045EAB1` | Comlink Send | `0x0045F19B`, 0 | `0x0045FD4D`, `0x0045FD63` | `fn_00418821` at `0x0045F5AA` and `0x0045F66F` (Cancel, Send) | Caret counter; every third step switches the caret |
| `fn_00430C23` | Detailed Combat | `0x00430DF4`, 0 | `0x004310EA`, `0x00431100` | `fn_00418821` at `0x00430FA1` (Exit) | Clip timeline tick |
| `fn_00448718` | Idle-gang warning | `0x0044879D`, 0 | `0x00448C55`, `0x00448C6B` | `fn_00418821` at `0x0044899F` and `0x00448A3F` (Cancel, OK) | Line countdown: 6 steps shown, 2 dark |

- In `fn_0044F2FC`, a page helper call that returns nonzero sets the counter to
  0, redraws the page through `fn_0044FD6C` and copies it to the screen before
  the slot 0 test of the same pass. The key paths call `fn_00451602` with a
  second argument of 1 (`0x0044F506`, `0x0044F5E5`), which waits through
  `fn_00464CD9(1)`; that wait reads and clears slot 0 (`0x00464D1C`,
  `0x00464CEB`, `0x00464D35`).
- In `fn_00449E80`, the three calls of `fn_0044B699` (Item Information) are each
  followed by setting the counter to 0.
- A held face that accepts sets the loop's exit flag; the slot 0 test still
  runs once in that pass before the loop ends and the panel slides out.
- The Research handler `fn_004427FA` calls the pump with a second argument of 1
  (`0x00442A6E`) and calls neither `fn_004328BE` nor `fn_004328F8`. The panel
  readers of slot 0 are the six panels of the table, the gang information panel
  `fn_00449E80` among them; Research is not one.
- Besides the pump, `fn_00464CD9` and the eight functions of the table, the
  readers of slot 0 are `fn_0040CED0`, `fn_0046A7CB`, `fn_0046D22F`,
  `fn_004677F0` and `fn_00456F80`. None of them calls `fn_00418821`.

## Interpretation

While one of these panels' faces or page arrows is held under the pointer, the
panel's loop is inside the helper and its animation stops: the item rotations,
the researched item of Last Turn Events, the Comlink Send caret, the Detailed
Combat clip and the idle-gang warning's line. The helper leaves slot 0 alone, so
the flag keeps one tick of the hold, and the pass that ends with the release
takes it: the animation steps once, and the other ticks of the hold are lost.
This is what the event pump does after a hold (FND-UI-046), and the panels'
pump calls with a second argument of 0 leave the flag for the panel to take.

A page turned with a held arrow starts its researched item at frame 0 and, when
a tick fell during the hold, steps to frame 1 in the same pass, so frame 0 is
not seen. A page turned with a key starts at frame 0 and steps on the next
tick, because the wait cleared the flag.

The gang information panel's items start again from frame 0 each time the Item
Information panel opened from it closes.

## Alternatives

- The hold loops of the network setup screens (`fn_00438DA5`, `fn_00468E12` and
  `fn_00457EED`, called from `fn_004677F0` and `fn_00456F80`) were not read, so
  whether their slot 0 steps stop in the same way is not recorded.

## How to reproduce

List the calls of `0x004328BE`, `0x004328F8`, `0x00418821`, `0x00451602` and
`0x00462579` with the values pushed before each. In each function of the table,
find the loop around the pump call, the switch on the event type that follows,
and the slot 0 test after the switch. In `0x0044F2FC` follow the branches on
`g_004948F8` and the page helper's return; in `0x00449E80` the code after each
call of `0x0044B699`.
