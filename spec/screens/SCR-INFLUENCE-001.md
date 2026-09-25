---
id: SCR-INFLUENCE-001
title: Influence picker for choosing one of the sector's three sites
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-INFLUENCE-001, FND-TURN-001, FND-PLATFORM-002]
conflicting: []
split_with: []
related: [RULE-INFLUENCE-001]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX16/PX05005` | None | Not recorded | The picker is open | FND-INFLUENCE-001 |

## Mouse input

Rectangles are in the shared panel's own coordinates; the panel's position on
the screen is an open question.

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Site slot 0 | Panel `(106, 17, 120, 64)` | The acting gang's sector's site slot 0 has progress different from its definition's Resistance | A click selects slot 0 as the Influence target; a double-click opens the Site Information panel for that site | FND-INFLUENCE-001, FND-TURN-001 |
| Site slot 1 | Panel `(208, 73, 120, 64)` | The same test for slot 1 | A click selects slot 1; a double-click opens the Site Information panel for that site | FND-INFLUENCE-001, FND-TURN-001 |
| Site slot 2 | Panel `(106, 127, 120, 64)` | The same test for slot 2 | A click selects slot 2; a double-click opens the Site Information panel for that site | FND-INFLUENCE-001, FND-TURN-001 |
| Cancel control | Not recorded | The picker is open | Closes the picker without giving an order | FND-INFLUENCE-001 |
| Confirmation control | Not recorded | A site slot is selected | Gives the gang the Influence order on the selected slot, which RULE-INFLUENCE-001 carries out | FND-INFLUENCE-001 |

## Keyboard input

None known.

## Other input

None.

## Sounds

None known.

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Nothing selected | The picker opens | An enabled site slot is clicked, or the picker closes | FND-INFLUENCE-001 |
| Site selected | An enabled site slot is clicked | Another enabled slot is clicked, or the picker closes by Cancel or confirmation | FND-INFLUENCE-001 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- The shared panel's origin on the screen is not recorded in a finding, so
  the rectangles are given in panel coordinates. If the panel is drawn at
  `(104, 125)`, as the acting-gang portrait position of other command panels
  suggests, slot 0 is `(210, 142, 120, 64)` on the screen.
- The rectangles differ from the staggered site pictures the panel draws; the
  pictures' source (`DATA/PX16/PX02000` strips of the sector's sites is the
  lead) and positions are not recorded.
- The positions of the Cancel and confirmation controls, and whether Enter and
  Escape work here, are not recorded.
- The Site Information panel (`DATA/PX16/PX05002`) belongs to another area; its
  screen ID should be named in the effects once it exists.
- When the game runs with the 8-bit image set it uses the `DATA/PX08` files of
  the same names (FND-PLATFORM-002).
- Which gang field the confirmation writes the slot into is not recorded;
  RULE-INFLUENCE-001 assumes `target`.
