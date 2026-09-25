---
id: SCR-INFLUENCE-001
title: Influence picker for choosing one of the sector's three sites
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-INFLUENCE-001, FND-INFLUENCE-002, FND-INFLUENCE-003, FND-RESEARCH-005, FND-TURN-001, FND-AUDIO-011, FND-EXE-004, FND-PLATFORM-002]
conflicting: []
split_with: []
related: [RULE-INFLUENCE-001]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX16/PX05005` | None | `(104, 124, 344, 209)` | The picker is open | FND-INFLUENCE-001, FND-INFLUENCE-002 |
| Gang portrait | `DATA/PX16/PX03000`, the 64-by-64 cell of the acting gang's definition | The acting gang | Panel `(26, 17, 64, 64)` | The picker is open | FND-INFLUENCE-003 |
| Site picture, slot `s` | `DATA/PX16/PX02000`, the 120-by-64 cell of the slot's site definition, opaque | The sector's site in slot `s` | Panel `(106, 17, 120, 64)`, `(208, 73, 120, 64)` and `(106, 127, 120, 64)` for slots 0, 1 and 2 | The picker is open, for a site not completed | FND-INFLUENCE-002 |
| Completed site | The same cell through the pattern mask on black, under the keyed `DATA/PX16/PX00129` frame `(362,299,120,64)` | A site whose progress equals its Resistance | As the site picture | The site is completed | FND-INFLUENCE-002 |
| Site frame | `DATA/PX16/PX00129` `(242,299,120,64)`, keyed on exact white | None | Over each site picture | For each site not completed | FND-INFLUENCE-002 |
| Chosen site | The site picture with the keyed `DATA/PX16/PX00129` frame `(0,235,120,64)` over it, prepared off screen | The chosen slot | Over the chosen slot | Once a slot is chosen | FND-INFLUENCE-002 |

## Mouse input

Rectangles are in the shared panel's own coordinates; the panel is at
`(104,124)` on the screen [FND-INFLUENCE-002].

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Site slot 0 | Panel `(106, 17, 120, 64)` | The acting gang's sector's site slot 0 has progress different from its definition's Resistance | A click selects slot 0 as the Influence target; a double-click opens the Site Information panel for that site and leaves the selection as it was | FND-INFLUENCE-001, FND-INFLUENCE-003, FND-TURN-001 |
| Site slot 1 | Panel `(208, 73, 120, 64)` | The same test for slot 1 | As slot 0, for slot 1 | FND-INFLUENCE-001, FND-INFLUENCE-003, FND-TURN-001 |
| Site slot 2 | Panel `(106, 127, 120, 64)` | The same test for slot 2 | As slot 0, for slot 2 | FND-INFLUENCE-001, FND-INFLUENCE-003, FND-TURN-001 |
| Cancel control | Panel `(33, 137, 49, 22)` | The picker is open | On release inside, closes the picker and leaves the gang's order as it was | FND-INFLUENCE-001, FND-INFLUENCE-002, FND-INFLUENCE-003 |
| Confirmation control | Panel `(33, 169, 49, 22)` | A site slot is selected; otherwise a press plays the rejected sound | Writes the selected slot to the gang's `target` and closes the picker, and the command box that opened it sets the gang's `action` to Influence; RULE-INFLUENCE-001 carries the order out | FND-INFLUENCE-001, FND-INFLUENCE-002, FND-INFLUENCE-003 |
| Outside the panel | Anywhere outside `(104, 124, 344, 209)` on the screen | Always | Plays the rejected sound | FND-INFLUENCE-002 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| Enter or Execute | The picker is open | With a slot selected, draws the pressed confirmation face for one tick with `fn_00418CCC`, then as the confirmation control; otherwise plays the rejected sound | FND-INFLUENCE-002, FND-INFLUENCE-003, FND-RESEARCH-005 |
| Escape | The picker is open | Draws the pressed Cancel face for one tick with `fn_00418CCC`, then as the Cancel control | FND-INFLUENCE-002, FND-INFLUENCE-003, FND-RESEARCH-005 |

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Rejected input | `DATA/SND00204` (effect slot 4) | A press or double-click outside the panel, or confirmation with nothing selected | FND-INFLUENCE-003, FND-AUDIO-011 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Nothing selected | The picker opens for a gang whose `action` is not Influence | An enabled site slot is clicked, or the picker closes | FND-INFLUENCE-001, FND-INFLUENCE-003 |
| Site selected | An enabled site slot is clicked, or the picker opens for a gang whose `action` is already Influence, with its `target` selected | Another enabled slot is clicked, or the picker closes by Cancel or confirmation | FND-INFLUENCE-001 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- The site pictures sit exactly on the input rectangles, so slot 0 is
  `(210, 141, 120, 64)` on the screen (FND-INFLUENCE-002, FND-INFLUENCE-003).
  What the three `PX00129` frames look like has not been checked against the
  image.
- The Site Information panel (`DATA/PX16/PX05002`) belongs to another area; its
  screen ID should be named in the effects once it exists.
- When the game runs with the 8-bit image set it uses the `DATA/PX08` files of
  the same names (FND-PLATFORM-002).
