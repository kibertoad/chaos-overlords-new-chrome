---
id: FND-COMLINK-011
title: Comlink Send offers only other human players as recipients and has six recipient cells, Cancel and Send
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045EAB1
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045FDF1
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00418821
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004718EE
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The Send handler `fn_0045EAB1` is the lower (Send) half of the Comlink
  control in `fn_004718EE`. It loads `PX05018`, or resource 5023 as an
  alternative network template.
- It builds six eligibility bytes from the per-player enabled array and the
  `controller` array, clears the byte of the active player, and when no byte
  is left set plays general-effect slot 4 and does not open the panel.
- Its six recipient controls are half-open and panel-local:
  `(98,20)-(198,52)`, `(98,54)-(198,86)`, `(98,88)-(198,120)`,
  `(219,20)-(319,52)`, `(219,54)-(319,86)` and `(219,88)-(319,120)`. A click on
  an eligible recipient flips a selection byte of its own at once; a click on
  an ineligible one is rejected.
- Cancel is `(33,137)-(82,159)` and Send is `(33,169)-(82,191)`. Both go
  through the shared held-button helper `fn_00418821` (FND-UI-062), Cancel as
  kind 1 (`0x0045F5AA`) and Send as kind 0 (`0x0045F66F`), with a 50-by-23
  destination one pixel larger than the control. While the button is held
  inside, the helper shows the lit face from `PX00129`, `(0,409,50,23)` for
  Cancel and `(0,386,50,23)` for Send; while the pointer is outside, and after
  the button comes up wherever it is released, it shows the plain face,
  `(50,409,50,23)` and `(50,386,50,23)`. It plays slot 3 on the press, and
  the panel acts only on a release inside. Send with no recipient selected is
  rejected before the helper is entered.
- Only the Execute key (`0x2B`) sends, and it is rejected when no recipient is
  selected.
- The renderer `fn_0045FDF1` draws all six player cards, including the active
  player's and those of computer or empty slots. It uses the eligibility byte
  only to draw a card dimmed. A card's backing is black, and green while its
  selection byte is set.

## Interpretation

A recipient target is the 100-by-32 name and portrait area, inset one pixel
inside a 105-by-34 card. Enter does not send a message (FND-COMLINK-010).

## Alternatives

- The finding names the enabled array without giving its address, so whether
  it is `player_active` or a setup-time slot flag is not recorded.
- Which `controller` values count as human (0, 3 or both) is not recorded.

## How to reproduce

From the lower-half branch of the Comlink tile in `fn_004718EE`, open
`fn_0045EAB1`. Read the loop over six players that fills the eligibility
bytes, the rectangle constants, the calls to `fn_00418821` with kinds 1 and
0, and the virtual-key switch; read the two rectangles of each kind in
`fn_00418821` (FND-UI-062).
