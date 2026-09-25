---
id: SCR-EVENT-001
title: Last Turn Events panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-EVENT-001, FND-EVENT-002, FND-EVENT-003, FND-UI-016, FND-UI-012, FND-AUDIO-002, FND-AUDIO-011, FND-COMLINK-002, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-EVENT-005]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX16/PX05010` | None | (104, 124, 344, 209), the standard panel position | While the panel is open | FND-EVENT-002, FND-COMLINK-002 |
| Status line | One of `Chaos Overlords.exe#STRING/33` to `Chaos Overlords.exe#STRING/44`, chosen by the report's type | None | Not recorded | For every report | FND-EVENT-001 |
| Illustration | `DATA/PX16/PX06001` to `DATA/PX16/PX06009`: resource number 6000 plus the report type | None | A 242-by-158 area; its position is not recorded | The report's type is not 4 or 5 | FND-EVENT-003 |
| Site picture | The rectangle (12, row + 1, 94, 62) of `DATA/PX16/PX02000`, where row is the top of the completed site's 64-pixel row, stretched to 242 by 158 by `StretchBlt` in `COLORONCOLOR` mode, then combined with the 8-by-8 pattern `Chaos Overlords.exe#BITMAP/146`, which keeps the pixels at x 0 and 4 of even rows and x 2 and 6 of odd rows and turns the other 75 percent black | None | The 242-by-158 illustration area | The report's type is 4 | FND-UI-016 |
| Site overlay | `DATA/PX16/PX06004`, drawn after the pattern, so the pattern does not touch it | None | Over the illustration area; exact position not recorded | The report's type is 4 | FND-UI-016 |
| Research monitor | `DATA/PX16/PX06005` | None | The illustration area; exact position not recorded | The report's type is 5 | FND-UI-012 |
| Researched item | One 48-by-48 frame of the item's 720-by-48 `DATA/PX16/PX04xxx` strip, copied as it is, without centring | None | A 48-by-48 area inside the monitor; position not recorded | The report's type is 5 | FND-UI-012 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Previous | (135, 157, 26, 23) | Always | Shows the previous report; on the first report, changes nothing and plays the rejected-input sound | FND-EVENT-002, FND-COMLINK-002 |
| Next | (163, 157, 26, 23) | Always | Shows the next report; on the last report, changes nothing and plays the rejected-input sound | FND-EVENT-002, FND-COMLINK-002 |
| Exit | (137, 293, 49, 22) | Always | Closes the panel. No report is deleted | FND-EVENT-002, FND-COMLINK-002 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| Left | Always | As Previous | FND-EVENT-002, FND-AUDIO-011 |
| Right | Always | As Next | FND-EVENT-002, FND-AUDIO-011 |
| Enter | Always | Presses Exit | FND-EVENT-002 |
| Execute (virtual key `0x2B`) | Always | Presses Exit | FND-EVENT-002 |

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Accepted input | `DATA/SND00203` | A step to another report, before the pressed arrow is drawn | FND-EVENT-002, FND-AUDIO-011 |
| Rejected input | `DATA/SND00204` | Previous on the first report or Next on the last | FND-EVENT-002, FND-AUDIO-011 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Showing report `p` | RULE-EVENT-005 opens the panel (at `p` 0), or a legal step | Another step, or the panel closes | FND-EVENT-001, FND-EVENT-002 |
| Closed | Exit is released inside itself, or Enter or Execute is pressed | The Events control is pressed, or the next turn starts with reports | FND-EVENT-002, SRC-MANUAL-GOG |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- FND-EVENT-002 converts pointer positions within "the shared panel" without
  restating its origin. The rectangles use the standard panel origin (104, 124)
  that FND-COMLINK-002 and FND-SEARCH-002 record for the same panel position.
- The positions of the status line, the illustration area, the page counter
  and the footer fields are not recorded. The page counter and footer fields
  are not described by any finding.
- Which string resource belongs to which report type is not recorded.
- The resource and position of the pressed arrow faces are not recorded.
- Which frame of the item strip the research report shows, and whether it
  animates, is not recorded.
- The manual (numbered page 23) says the panel opens by itself at the start of
  a turn with reports and that closing it before every report has been seen
  makes a yellow light blink on the Events button. No finding covers either.
- Resources are given from `DATA/PX16/`; at 8-bit colour depth the game loads
  the file of the same name from `DATA/PX08/`.
