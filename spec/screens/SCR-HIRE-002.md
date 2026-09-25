---
id: SCR-HIRE-002
title: Hire offers on the main console, with drag-to-hire and Reject
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-HIRE-004, FND-HIRE-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-HIRE-003, RULE-HIRE-002]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Offer portrait, one per offer slot | `DATA/PX16/PX03000` | The portrait of the gang definition in the player's entry of `hire_offers` for that slot (RULE-HIRE-002) | Not recorded | During the player's `planning_phase` | SRC-MANUAL-GOG |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Offer portrait, dragged and dropped on a sector | Not recorded | During the player's `planning_phase` | Orders that offer hired into the sector and clears the other two slots' orders (RULE-HIRE-003) | FND-HIRE-004 |
| Reject control under an offer | Not recorded | During the player's `planning_phase` | Toggles that offer between no order and a snub, or cancels its hire (RULE-HIRE-003) | FND-HIRE-004 |

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

- Positions and rectangles are not recorded in a finding. Unconfirmed leads
  from captures of the original: three 66-pixel cells starting at
  `(438, 370)` with 64-by-64 portraits at x 439, 505 and 571, a Reject target
  of `(472 + 66 * slot, 437, 32, 13)`, a stamp from `DATA/PX16/PX00129` at
  `(120, 300, 60, 60)` over an offer ordered to hire and a red cross from
  `(180, 300, 60, 60)` over a snubbed one, and each offer's hire cost in the
  left half of its cell's footer.
- Which sectors accept the drop is not recorded; the manual (page 17) says a
  sector the player controls or one holding one of the player's gangs.
- Whether a double-click on an offer opens the gang definition panel
  (`DATA/PX16/PX05022`, FND-GANG-002) and from which handler is not recorded.
- Whether any sound plays on a drop or on Reject is not recorded.
- Whether this belongs in the main console's screen entry of the UI area is
  for that entry to decide; the offers are described here because their input
  is the hire order.
