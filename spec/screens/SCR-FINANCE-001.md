---
id: SCR-FINANCE-001
title: Financial panel, City and Sector
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-FINANCE-001, FND-OPTIONS-001, FND-GANG-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-FINANCE-001, RULE-UI-003, RULE-UI-004]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel, City | `DATA/PX08/PX05008`, the 320-pixel part from buffer x = 344 | None | `(128, 124, 320, 209)` | When opened as City | FND-FINANCE-001 |
| Panel, Sector | `DATA/PX08/PX05019`, the same crop | None | `(128, 124, 320, 209)` | When opened as Sector | FND-FINANCE-001, SRC-MANUAL-GOG |
| Overlord portrait | Not recorded | The viewing player's portrait | `(154, 141, 64, 64)` | While the panel is open | FND-FINANCE-001 |
| Value fields | Not recorded | The amounts of RULE-FINANCE-001, each drawn by `number_cells` (RULE-UI-004) in four glyph cells from the field's left edge | `(394, y, 24, 7)` for y = 151, 160, 178, 196, 214, 223, 241 and 268 | While the panel is open | FND-FINANCE-001, FND-GANG-004 |
| Contract count | Not recorded | The number of gangs hired this turn (RULE-FINANCE-001): one bright cell at x = 316 below ten, two cells from x = 316 otherwise, then a closing parenthesis at x = 322 or 328. The opening parenthesis is part of the panel image | Row not recorded | While the panel is open | FND-FINANCE-001 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Close control | `(161, 293, 49, 22)` | Always | Closes the panel | FND-FINANCE-001 |

## Keyboard input

None known.

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Panel opening | General effect slot 0, as RULE-UI-003 gives | The panel slides in, with Slide Panels on | FND-OPTIONS-001 |
| Panel closing | General effect slot 1, as RULE-UI-003 gives | The panel slides out, with Slide Panels on | FND-OPTIONS-001 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| City | The player opens Financial and picks City on the main console | The close control is clicked | FND-FINANCE-001, SRC-MANUAL-GOG |
| Sector | The player opens Financial and picks Sector | The close control is clicked | FND-FINANCE-001, SRC-MANUAL-GOG |

## Timing

The panel slides in and out in the alternate 320-pixel form, in about a
quarter of a second (FND-OPTIONS-001), as RULE-UI-003 describes.

## Differences between builds

None known.

## Open questions

- Which row holds which amount. SRC-MANUAL-GOG, pages 23 and 24, lists Gang
  Upkeep, New Recruits, Equipment, City Officials, Sector Tax, Site Protection,
  Chaos (Estimate) and Cash Adjustment; the eight recorded rows presumably hold
  them in that order.
- How red costs and green income are chosen per field, and the glyph resource
  the helper copies.
- The row of the contract count.
- Which sector the Sector variant shows, and how the handler picks `PX05019`.
- Which keys close the panel.
- The Overlord portrait's resource.
