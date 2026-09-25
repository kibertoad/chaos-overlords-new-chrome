---
id: SCR-FINANCE-001
title: Financial panel, City and Sector
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-FINANCE-001, FND-FINANCE-002, FND-OPTIONS-001, FND-GANG-004, SRC-MANUAL-GOG, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-FINANCE-001, RULE-UI-003, RULE-UI-004]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel, City | `DATA/PX08/PX05008`, the 320-pixel part from buffer x = 344 | None | `(128, 124, 320, 209)` | When opened as City | FND-FINANCE-001 |
| Panel, Sector | `DATA/PX08/PX05019`, the same crop | None | `(128, 124, 320, 209)` | When opened as Sector | FND-FINANCE-001, FND-FINANCE-002, SRC-MANUAL-GOG |
| Overlord portrait | Not recorded | The viewing player's portrait | `(154, 141, 64, 64)` | While the panel is open | FND-FINANCE-001 |
| Value fields | Not recorded | The amounts of RULE-FINANCE-001, each drawn by `number_cells` (RULE-UI-004) in four glyph cells from the field's left edge: Gang Upkeep at y = 151, New Recruits 160, Equipment 178, City Officials 196, Sector Tax 214, Site Protection 223, Chaos (Estimate) 241 and Cash Adjustment 268 | `(394, y, 24, 7)` | While the panel is open | FND-FINANCE-001, FND-FINANCE-002, FND-GANG-004 |
| Gang count | Not recorded | The number of gangs counted on the Gang Upkeep row, queued hires included (RULE-FINANCE-001): one bright cell at x = 316 below ten, two cells from x = 316 otherwise, then a closing parenthesis at x = 322 or 328. The opening parenthesis is part of the panel image | On the Gang Upkeep row, y = 151 | While the panel is open | FND-FINANCE-001, FND-FINANCE-002 |
| Sector name | Not recorded | In the Sector variant, the column letter and row digit of the sector | Buffer `(396, 216)`, screen `(180, 196)` | When opened as Sector | FND-FINANCE-002 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Close control | `(161, 293, 49, 22)` | Always | Closes the panel | FND-FINANCE-001 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| `Enter` or `Execute` (virtual key `0x2B`) | Always | Presses the close control | FND-FINANCE-002 |

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
| Sector | The player opens Financial and picks Sector; the panel shows the sector selected on the map | The close control is clicked | FND-FINANCE-001, FND-FINANCE-002, SRC-MANUAL-GOG |

## Timing

The panel slides in and out in the alternate 320-pixel form, in about a
quarter of a second (FND-OPTIONS-001), as RULE-UI-003 describes.

## Differences between builds

None known.

## Open questions

- The row names above pair the eight drawn values, top to bottom, with the
  rows SRC-MANUAL-GOG lists on pages 23 and 24; the labels themselves are in
  the panel images, which were not read (FND-FINANCE-002).
- How red costs and green income are chosen per field, and the glyph resource
  the helper copies.
- The Overlord portrait's resource.
