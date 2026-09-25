---
id: SCR-HIRE-002
title: Hire offers on the main console, with drag-to-hire and Reject
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-HIRE-004, FND-HIRE-001, FND-HIRE-007, FND-HIRE-008, FND-EXE-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-HIRE-003, RULE-HIRE-002, SCR-GANG-001]
---

## Drawn elements

Rectangles are `(x, y, width, height)` in screen coordinates; `s` is the offer
slot, 0 to 2.

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Offer portrait, one per offer slot | `DATA/PX16/PX03000` | The portrait of the gang definition in the player's entry of `hire_offers` for that slot (RULE-HIRE-002), picked by the definition's portrait number, ten to a row of the sheet | `(440 + 66s, 373, 64, 64)` | During the player's `planning_phase` | FND-HIRE-007, FND-HIRE-008 |
| Offer price | Digit glyphs of `DATA/PX16/PX00129` | The definition's `hire_cost`, two cells wide | Left edge x `450 + 66s`, top y 440 | During the player's `planning_phase` | FND-HIRE-007 |
| Hire mark | `DATA/PX16/PX00129`, the 64-by-64 image at `(114, 299)` | None | Over the portrait, transparent | The slot's order is a sector | FND-HIRE-008 |
| Snub mark | `DATA/PX16/PX00129`, the 64-by-64 image at `(178, 299)` | None | Over the portrait, transparent | The slot's order is -2 | FND-HIRE-008 |
| Dragged portrait | `DATA/PX16/PX03000` | The dragged offer's portrait, 40 by 40 | Under the pointer, which is held to x 20 to 620 and y 20 to 440 | While an offer is dragged | FND-HIRE-008 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Offer portrait, pressed and dragged | Slot 0 when x is 440 to 504, slot 1 above 504 to 570, slot 2 above 570 to 636, y 373 to 436 | During the player's `planning_phase` | Dragging starts once the pointer moves more than 2 pixels from the press point; releasing first ends with no change. A drop inside `(2, 42, 432, 416)` names a sector: in the city map the cell under the pointer, 54 by 52 pixels, eight to a row; in the sector view one of the 3-by-3 cells of `(64, 60, 162, 156)`, the shown sector for a point elsewhere in the map area. The drop is accepted when the player owns the sector or has a living gang there; it then orders the offer hired into that sector and clears the other two slots' orders (RULE-HIRE-003). Any other drop changes nothing | FND-HIRE-008 |
| Offer portrait, double-clicked | As above | During the player's `planning_phase` | Opens the live-gang panel (SCR-GANG-001) on a Force 0 gang of the offered definition with no equipment | FND-HIRE-008 |
| Reject control under an offer | `(472 + 66s, 437, 32, 13)` | During the player's `planning_phase` | Toggles that offer between no order and a snub, or cancels its hire, and clears the other two slots' orders (RULE-HIRE-003) | FND-HIRE-008 |

## Keyboard input

None known.

## Other input

None.

## Sounds

None known.

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| No order | Planning starts (every order is -1 after the last `hire_phase`), or Reject cancels a snub or a hire | An offer is dropped on a sector or Reject is pressed | FND-HIRE-004, FND-HIRE-001 |
| One offer ordered to hire | An offer is dropped on a sector | Another drop, or Reject on any slot | FND-HIRE-004 |
| One offer snubbed | Reject is pressed on a slot with no order | Reject is pressed on it again, or any offer is dropped on a sector | FND-HIRE-004 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- In the sector view a cell of the 3-by-3 grid whose neighbour does not exist
  ends the drop; the table that says which cells exist is the sector view's
  (FND-HIRE-008).
- Whether any sound plays on a drop or on Reject is not recorded.
- Whether this belongs in the main console's screen entry of the UI area is
  for that entry to decide; the offers are described here because their input
  is the hire order.
