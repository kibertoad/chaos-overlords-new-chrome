---
id: SCR-SEARCH-001
title: Search panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-SEARCH-001, FND-SEARCH-002, FND-SEARCH-004, FND-COMLINK-007, FND-AUDIO-002, FND-AUDIO-011, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-SEARCH-001, RULE-SEARCH-002]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX16/PX05024` | None | (104, 124, 344, 209) | While the panel is open | FND-SEARCH-001, FND-SEARCH-002 |
| Row `n`, for site definitions 0 to 21 | The 20-by-14 cell `(20 * (n % 11), 14 * (n / 11))` of `DATA/PX16/PX00150`, keyed on exact white, then the first 15 characters of the site definition's name in the plain font of `DATA/PX16/PX00129` (row y 0) when the row is selected and in the font row at `(152,274)` when it is not | Whether `search_filters` selects definition `n` for the active player (RULE-SEARCH-001) | Icon at `(206 + 116 * (n / 11), 146 + 15 * (n % 11))`, name 24 pixels right and 3 down | While the panel is open; drawn again after ALL, NONE or a click on the row | FND-SEARCH-001, FND-SEARCH-002, FND-SEARCH-004 |
| ALL pressed | `DATA/PX16/PX00129` rectangle (147, 560, 50, 23); plain face (97, 560, 50, 23) | None | (137, 140, 50, 23) | While ALL is held with the pointer inside it | FND-SEARCH-002, FND-SEARCH-004 |
| NONE pressed | `DATA/PX16/PX00129` rectangle (247, 560, 50, 23); plain face (197, 560, 50, 23) | None | (137, 172, 50, 23) | While NONE is held with the pointer inside it | FND-SEARCH-002, FND-SEARCH-004 |
| Done pressed | `DATA/PX16/PX00129` rectangle (50, 386, 50, 23); plain face (0, 386, 50, 23) | None | (137, 293, 50, 23) | While Done is held with the pointer inside it | FND-SEARCH-004, FND-COMLINK-007 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Outside the panel | Anything outside (104, 124, 344, 209) | Always | A press plays the rejected-input sound; a double-click does nothing | FND-SEARCH-004 |
| ALL | (137, 140, 49, 23) | Always | On release inside, `search_set_all(1)` (RULE-SEARCH-001), then draws every row again | FND-SEARCH-001, FND-SEARCH-002, FND-SEARCH-004 |
| NONE | (137, 172, 49, 23) | Always | On release inside, `search_set_all(0)` (RULE-SEARCH-001), then draws every row again | FND-SEARCH-001, FND-SEARCH-002, FND-SEARCH-004 |
| Done | (137, 293, 49, 22) | Always | On release inside, closes the panel; the city then draws its markers by RULE-SEARCH-002 | FND-SEARCH-002 |
| Row `n`, press | (206 + 116 * (n / 11), 146 + 15 * (n % 11), 114, 15) | Always | `search_toggle(n)` (RULE-SEARCH-001) at once, then draws the row again | FND-SEARCH-001, FND-SEARCH-002, FND-SEARCH-004 |
| Row `n`, double-click | As above | Always | Opens the Site Information panel, `DATA/PX16/PX05002`, for site definition `n`, and returns to Search when it closes. The first press of the double-click has already flipped the row, and the double-click does not flip it back | FND-SEARCH-001, FND-SEARCH-002, FND-SEARCH-004 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| Enter | Always | Presses Done | FND-SEARCH-002 |
| Execute (virtual key `0x2B`) | Always | Presses Done | FND-SEARCH-002 |

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Accepted input | `DATA/SND00203` | ALL, NONE or Done is pressed | FND-SEARCH-002, FND-SEARCH-004, FND-AUDIO-011 |
| Rejected input | `DATA/SND00204` | A press outside the panel | FND-SEARCH-004, FND-AUDIO-011 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Open | The Search part of the Ranking and Search control is pressed | Done is released inside itself, or Enter or Execute is pressed | FND-SEARCH-002 |
| Site Information over Search | A row is double-clicked | The Site Information panel closes | FND-SEARCH-001, FND-SEARCH-002 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- What the two font rows look like (which one reads as selected) has not
  been checked against `PX00129` (FND-SEARCH-004).
- The Site Information panel belongs to the interface area; its screen entry
  is not yet cross-referenced here.
