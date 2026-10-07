---
id: FND-ATTACK-005
title: The Attack picker builds each target card from a 66-by-87 PX00129 frame with the gang's portrait, a Force track and its item icons
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043D132
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043D2B7..0x0043D8C7
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_0043D132(opponent, sector)` (range in FND-EXE-004) is the list builder the
Attack picker calls for the chosen opponent (FND-ATTACK-003). Surface 6 holds
`PX00129` (FND-UI-031), surface 3 the gang portraits and surface 5 the item
icons (FND-ATTACK-003). Rectangles are `(left, top)-(right, bottom)`.

- It loads `PX05003` into surface 7 at `(344,144)-(688,353)`, a clean copy of
  the panel, and copies that copy's target area `(479,160)-(681,337)` over the
  working panel's target area `(135,160)-(337,337)`, so the cards of the
  previous opponent are erased.
- For each gang the list takes, it composes a card at `(0,0)-(66,87)` of
  surface 7 and then copies that card to the target cell:
  - the card frame, surface 6 `(0,299)-(66,386)` (`0x0043D2B7..0x0043D2ED`);
  - the 64-by-64 portrait cell of surface 3 chosen by the definition's word at
    `0x004A281E + definition * 0x9C`, at `(1,1)-(65,65)`;
  - a black fill of `(2,58)-(64,63)` over the bottom of the portrait
    (`0x0043D43F`);
  - the 60-by-3 red track, surface 6 `(354,3)-(414,6)`, at `(3,59)-(63,62)`,
    and over it the first `6 * Force` pixels of the green strip at surface 6
    `(354,0)`, from the same left edge (`0x0043D4B4..0x0043D564`). Force is
    byte 3 of the gang record (FND-STATE-002);
  - the 20-by-20 icon of each of bytes 4, 5 and 6 that is not -1, from surface
    5 at x 120 and row `20 * id`, at `(1,66)`, `(23,66)` and `(45,66)`
    (`0x0043D647..0x0043D811`).
- The card is copied to `(135 + 68 * (n % 3), 160 + 90 * (n / 3))` of surface
  7 for the `n`th listed gang (`0x0043D89B..0x0043D8C7`). With the panel at
  rows 144 to 353, that is panel-local `(135 + 68 * (n % 3), 16 + 90 * (n / 3))`.

## Interpretation

Each target card in the picker is the green frame with a portrait well and
three item boxes, the target's portrait, its Force as the same red-and-green
track the combat screens use (FND-COMBAT-009), and its equipment in the boxes.
The acting gang's portrait and items sit in boxes that are part of the panel
art; the picker draws no Force track for the acting gang.

## Alternatives

None known.

## How to reproduce

Find the call to `fn_0043D132` in the Attack picker `fn_0043B290`. In
`fn_0043D132`, read the copies from surface 6 at y 299 and x 354, the black
fill at `(2,58)`, the multiplication of byte 3 by 6, and the card's
destination built from `68 * (n % 3) + 135` and `90 * (n / 3) + 160`.
